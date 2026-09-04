using DomainCopilot.Domain.Documents;

namespace DomainCopilot.Application.VectorStore;

/// <summary>
/// One indexed chunk, matching the Qdrant payload schema fixed in ADR-0004. Knowledge-corpus only
/// — no policy/claim number or OCR fields; case data never reaches this store (ADR-0004).
/// </summary>
public sealed record VectorRecord(
    Guid DocumentId,
    int ChunkIndex,
    string SectionTitle,
    int? PageNumber,
    DocumentCategory Category,
    string? FormVersion,
    DateOnly? EffectiveDate,
    string ContentHash,
    string Text,
    ReadOnlyMemory<float> Embedding)
{
    /// <summary>Deterministic point id from document + chunk index, so re-ingesting the same
    /// document upserts each chunk in place instead of accumulating duplicates.</summary>
    public Guid PointId => DeterministicGuid($"{DocumentId:N}:{ChunkIndex}");

    private static Guid DeterministicGuid(string input)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash.AsSpan(0, 16));
    }
}
