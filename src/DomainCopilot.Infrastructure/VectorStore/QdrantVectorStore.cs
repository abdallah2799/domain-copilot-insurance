using DomainCopilot.Application.VectorStore;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace DomainCopilot.Infrastructure.VectorStore;

/// <summary>
/// The Qdrant adapter for <see cref="IVectorStore"/>. One collection for the whole knowledge
/// corpus (ADR-0004) — a claims-adjudication query legitimately needs policy wording + guidelines
/// + a specific endorsement together, and Qdrant's payload filtering is efficient enough at this
/// scale that splitting collections would only add complexity.
/// </summary>
public sealed class QdrantVectorStore(QdrantClient client) : IVectorStore
{
    public const string CollectionName = "domain_copilot_knowledge_chunks";

    public async Task EnsureCollectionAsync(int vectorSize, CancellationToken cancellationToken = default)
    {
        if (await client.CollectionExistsAsync(CollectionName, cancellationToken))
        {
            return;
        }

        await client.CreateCollectionAsync(
            CollectionName,
            new VectorParams { Size = (ulong)vectorSize, Distance = Distance.Cosine },
            cancellationToken: cancellationToken);
    }

    public async Task UpsertAsync(IReadOnlyList<VectorRecord> records, CancellationToken cancellationToken = default)
    {
        if (records.Count == 0)
        {
            return;
        }

        var points = records.Select(ToPointStruct).ToList();
        await client.UpsertAsync(CollectionName, points, cancellationToken: cancellationToken);
    }

    public async Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        if (!await client.CollectionExistsAsync(CollectionName, cancellationToken))
        {
            return;
        }

        Filter filter = Conditions.MatchKeyword("documentId", documentId.ToString());
        await client.DeleteAsync(CollectionName, filter, cancellationToken: cancellationToken);
    }

    private static PointStruct ToPointStruct(VectorRecord record)
    {
        var point = new PointStruct
        {
            Id = record.PointId,
            Vectors = record.Embedding.ToArray(),
        };

        point.Payload["documentId"] = record.DocumentId.ToString();
        point.Payload["chunkIndex"] = record.ChunkIndex;
        point.Payload["sectionTitle"] = record.SectionTitle;
        point.Payload["category"] = record.Category.ToString();
        point.Payload["contentHash"] = record.ContentHash;
        point.Payload["text"] = record.Text;

        if (record.PageNumber is { } page)
        {
            point.Payload["pageNumber"] = page;
        }

        if (record.FormVersion is { } formVersion)
        {
            point.Payload["formVersion"] = formVersion;
        }

        if (record.EffectiveDate is { } effectiveDate)
        {
            point.Payload["effectiveDate"] = effectiveDate.ToString("yyyy-MM-dd");
        }

        return point;
    }
}
