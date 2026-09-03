using Microsoft.Extensions.Diagnostics.HealthChecks;
using Qdrant.Client;

namespace DomainCopilot.Infrastructure.VectorStore;

/// <summary>Wired into /health/ready (not /health/live) — proves the Qdrant client actually
/// reaches the server, not just that the process is up.</summary>
public sealed class QdrantHealthCheck(QdrantClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await client.ListCollectionsAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Qdrant unreachable", ex);
        }
    }
}
