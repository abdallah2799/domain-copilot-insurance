namespace DomainCopilot.Infrastructure.VectorStore;

/// <summary>
/// Host/port, not a URL — the Qdrant .NET client talks gRPC (default port 6334), not the REST API
/// (port 6333) a browser or curl would hit. Keeping these as separate fields instead of a single
/// "QdrantUrl" string avoids someone plausibly-but-wrongly pointing this at the REST port later.
/// </summary>
public sealed class QdrantOptions
{
    public const string SectionName = "VectorStore:Qdrant";

    public string Host { get; set; } = "localhost";
    public int GrpcPort { get; set; } = 6334;
    public bool Https { get; set; }
    public string? ApiKey { get; set; }
}
