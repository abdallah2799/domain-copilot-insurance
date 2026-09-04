using DomainCopilot.Application.Retrieval;
using DomainCopilot.Application.VectorStore;
using DomainCopilot.Infrastructure.Persistence;
using DomainCopilot.Infrastructure.Persistence.Chunks;
using Microsoft.EntityFrameworkCore;

namespace DomainCopilot.Infrastructure.Retrieval;

/// <summary>
/// The keyword-retrieval leg of hybrid search (ADR-0005): chunk text lives in MSSQL, and every
/// query scores the (small, corpus-sized) filtered candidate set with BM25 at query time rather
/// than maintaining a separate search-engine index — this corpus is a few hundred chunks, well
/// within what an in-process scan handles in milliseconds, and it avoids standing up SQL Server
/// Full-Text Search (a separately-installed feature component the base container image doesn't
/// include) for a scale that doesn't need it.
/// </summary>
public sealed class EfCoreKeywordSearchIndex(DomainCopilotDbContext dbContext, Bm25Scorer scorer) : IKeywordSearchIndex
{
    public async Task IndexAsync(IReadOnlyList<VectorRecord> records, CancellationToken cancellationToken = default)
    {
        if (records.Count == 0)
        {
            return;
        }

        var rows = records.Select(r => new ChunkRecord
        {
            Id = Guid.NewGuid(),
            DocumentId = r.DocumentId,
            ChunkIndex = r.ChunkIndex,
            SectionTitle = r.SectionTitle,
            PageNumber = r.PageNumber,
            Category = r.Category,
            FormVersion = r.FormVersion,
            EffectiveDate = r.EffectiveDate,
            Text = r.Text,
        });

        dbContext.Chunks.AddRange(rows);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await dbContext.Chunks.Where(c => c.DocumentId == documentId).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ScoredChunk>> SearchAsync(
        string queryText,
        int topK,
        RetrievalFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var candidates = await ApplyFilter(dbContext.Chunks.AsNoTracking(), filter).ToListAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            return [];
        }

        var scored = scorer.Score([.. candidates.Select(c => c.Text)], queryText);

        return [.. scored
            .Take(topK)
            .Select(s =>
            {
                var row = candidates[s.Index];
                return new ScoredChunk(
                    row.DocumentId,
                    row.ChunkIndex,
                    row.SectionTitle,
                    row.PageNumber,
                    row.Category,
                    row.FormVersion,
                    row.EffectiveDate,
                    row.Text,
                    s.Score);
            })];
    }

    private static IQueryable<ChunkRecord> ApplyFilter(IQueryable<ChunkRecord> query, RetrievalFilter? filter)
    {
        if (filter is null)
        {
            return query;
        }

        if (filter.Category is { } category)
        {
            query = query.Where(c => c.Category == category);
        }

        if (filter.FormVersion is { } formVersion)
        {
            query = query.Where(c => c.FormVersion == formVersion || c.FormVersion == null);
        }

        return query;
    }
}
