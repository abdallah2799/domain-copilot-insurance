using DomainCopilot.Domain.Documents;

namespace DomainCopilot.Infrastructure.Persistence.Chunks;

/// <summary>
/// The relational copy of one indexed chunk, existing solely to back the keyword-search leg of
/// hybrid retrieval (ADR-0005) with real stored text to run BM25 against. Infrastructure-only — not
/// a Domain entity (it carries no business rules of its own) and not exposed outside
/// <c>EfCoreKeywordSearchIndex</c>; Application only ever sees it through <c>IKeywordSearchIndex</c>'s
/// <c>VectorRecord</c>/<c>ScoredChunk</c> DTOs, the same way Qdrant's own storage shape stays hidden
/// behind <c>IVectorStore</c>.
/// </summary>
public sealed class ChunkRecord
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string SectionTitle { get; set; } = string.Empty;
    public int? PageNumber { get; set; }
    public DocumentCategory Category { get; set; }
    public string? FormVersion { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public string Text { get; set; } = string.Empty;
}
