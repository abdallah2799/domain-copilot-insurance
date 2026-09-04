using DomainCopilot.Domain.Documents;

namespace DomainCopilot.Application.Ingestion;

public sealed record IngestKnowledgeDocumentRequest(
    string SourceId,
    string Title,
    DocumentCategory Category,
    DocumentFormat Format,
    string SourceFileName,
    string? FormVersion,
    DateOnly? EffectiveDate,
    byte[] Content);

public sealed record IngestionResult(string SourceId, IngestionStatus Status, int ChunkCount, string? ErrorMessage);
