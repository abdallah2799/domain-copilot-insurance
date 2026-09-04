using System.Security.Cryptography;
using System.Text;
using DomainCopilot.Application.Documents;
using DomainCopilot.Application.Providers;
using DomainCopilot.Application.Retrieval;
using DomainCopilot.Application.VectorStore;
using DomainCopilot.Domain.Documents;
using Microsoft.Extensions.Logging;

namespace DomainCopilot.Application.Ingestion;

/// <summary>
/// The FR-1 pipeline for the knowledge corpus (ADR-0004): extract -> clean -> chunk -> embed ->
/// index, idempotent on <see cref="Document.ContentHash"/>, with per-document status/failure
/// reporting via the <see cref="Document"/> entity itself. Indexes into both retrieval legs
/// (ADR-0005) — the dense vector store and the keyword search index — from the same chunk records,
/// so the two legs can never drift out of sync with each other.
/// </summary>
public sealed class KnowledgeIngestionService(
    IDocumentRepository documentRepository,
    IDocumentExtractor extractor,
    IEmbeddingService embeddingService,
    IVectorStore vectorStore,
    IKeywordSearchIndex keywordSearchIndex,
    KnowledgeChunker chunker,
    ILogger<KnowledgeIngestionService> logger)
{
    public async Task<IngestionResult> IngestAsync(IngestKnowledgeDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var contentHash = ComputeHash(request.Content);
        var existing = await documentRepository.FindBySourceIdAsync(request.SourceId, cancellationToken);

        Document document;
        if (existing is null)
        {
            document = Document.Create(request.SourceId, request.Title, request.Category, request.Format, request.SourceFileName, contentHash, request.FormVersion, request.EffectiveDate);
            await documentRepository.AddAsync(document, cancellationToken);
        }
        else if (!existing.NeedsReingestion(contentHash))
        {
            // Idempotent no-op (FR-1): unchanged content, nothing to do.
            return new IngestionResult(request.SourceId, existing.Status, existing.ChunkCount, existing.ErrorMessage);
        }
        else
        {
            document = existing;
            document.UpdateContent(request.Title, contentHash, request.FormVersion, request.EffectiveDate);
            // Remove stale chunks before re-indexing — a shrinking document must not leave old
            // trailing chunks behind with no corresponding content anymore.
            await vectorStore.DeleteByDocumentIdAsync(document.Id, cancellationToken);
            await keywordSearchIndex.DeleteByDocumentIdAsync(document.Id, cancellationToken);
        }

        document.BeginProcessing();
        await documentRepository.SaveChangesAsync(cancellationToken);

        try
        {
            using var stream = new MemoryStream(request.Content);
            var extracted = await extractor.ExtractAsync(stream, request.Format, cancellationToken);
            var cleaned = TextCleaner.Clean(extracted);
            var chunks = chunker.Chunk(cleaned);

            if (chunks.Count == 0)
            {
                throw new InvalidOperationException("Extraction produced no chunkable content — check the source file isn't empty or unparseable.");
            }

            var embeddings = await embeddingService.EmbedAsync([.. chunks.Select(c => c.Text)], cancellationToken);

            // Idempotent — checks existence before creating, so this is safe (and cheap) to call
            // on every ingestion run rather than requiring a separate one-time seed step.
            await vectorStore.EnsureCollectionAsync(embeddings[0].Length, cancellationToken);

            var records = chunks.Zip(embeddings, (chunk, embedding) => new VectorRecord(
                document.Id,
                chunk.ChunkIndex,
                chunk.SectionTitle,
                chunk.PageNumber,
                document.Category,
                document.FormVersion,
                request.EffectiveDate,
                ComputeHash(Encoding.UTF8.GetBytes(chunk.Text)),
                chunk.Text,
                embedding)).ToList();

            await vectorStore.UpsertAsync(records, cancellationToken);
            await keywordSearchIndex.IndexAsync(records, cancellationToken);

            document.MarkCompleted(chunks.Count);
            await documentRepository.SaveChangesAsync(cancellationToken);

            return new IngestionResult(request.SourceId, IngestionStatus.Completed, chunks.Count, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ingestion failed for {SourceId}", request.SourceId);
            document.MarkFailed(ex.Message);
            await documentRepository.SaveChangesAsync(cancellationToken);
            return new IngestionResult(request.SourceId, IngestionStatus.Failed, 0, ex.Message);
        }
    }

    private static string ComputeHash(byte[] content) => Convert.ToHexString(SHA256.HashData(content));
}
