namespace DomainCopilot.Application.Ingestion;

/// <summary>One structure-aware chunk (ADR-0004) ready to be embedded and indexed.</summary>
public sealed record KnowledgeChunk(int ChunkIndex, string SectionTitle, string Text, int? PageNumber);
