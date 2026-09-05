using DomainCopilot.Application.Observability;
using DomainCopilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DomainCopilot.Infrastructure.Observability;

/// <summary>Read-only, so (unlike <see cref="EfTokenUsageRecorder"/>) sharing the ambient scoped
/// <see cref="DomainCopilotDbContext"/> is fine -- there is no unrelated pending write to
/// accidentally flush.</summary>
public sealed class EfTokenUsageQueryService(DomainCopilotDbContext dbContext) : ITokenUsageQueryService
{
    public async Task<TokenUsageReport> GetReportAsync(int recentLimit = 100, CancellationToken cancellationToken = default)
    {
        var recent = await dbContext.TokenUsageRecords
            .OrderByDescending(r => r.TimestampUtc)
            .Take(recentLimit)
            .Select(r => new TokenUsageSummary(r.TimestampUtc, r.CorrelationId, r.AgentName, r.ProviderName, r.ModelName, r.PromptTokens, r.CompletionTokens, r.EstimatedCostUsd))
            .ToListAsync(cancellationToken);

        var totals = await dbContext.TokenUsageRecords
            .GroupBy(_ => 1)
            .Select(g => new
            {
                PromptTokens = g.Sum(r => r.PromptTokens),
                CompletionTokens = g.Sum(r => r.CompletionTokens),
                CostUsd = g.Sum(r => r.EstimatedCostUsd),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return new TokenUsageReport(recent, totals?.PromptTokens ?? 0, totals?.CompletionTokens ?? 0, totals?.CostUsd ?? 0m);
    }
}
