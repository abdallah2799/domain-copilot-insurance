using DomainCopilot.Application.Observability;
using DomainCopilot.Infrastructure.Persistence;
using DomainCopilot.Infrastructure.Persistence.Observability;
using Microsoft.EntityFrameworkCore;

namespace DomainCopilot.Infrastructure.Observability;

/// <summary>Uses its own short-lived <see cref="DomainCopilotDbContext"/> (via <see
/// cref="IDbContextFactory{TContext}"/>), not the ambient scoped context AgentRunner/AskService and
/// their repositories already share -- a cross-cutting write like this one calling
/// <c>SaveChangesAsync</c> on that shared context would flush whatever unrelated pending changes
/// happen to be tracked on it at that moment, which is exactly the kind of surprising side effect a
/// genuinely separate context avoids.</summary>
public sealed class EfTokenUsageRecorder(IDbContextFactory<DomainCopilotDbContext> dbContextFactory, ModelPricingOptions pricing) : ITokenUsageRecorder
{
    public async Task RecordAsync(TokenUsageEntry entry, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.TokenUsageRecords.Add(new TokenUsageRecord
        {
            Id = Guid.NewGuid(),
            TimestampUtc = DateTimeOffset.UtcNow,
            CorrelationId = entry.CorrelationId,
            AgentName = entry.AgentName,
            ProviderName = entry.ProviderName,
            ModelName = entry.ModelName,
            PromptTokens = entry.PromptTokens,
            CompletionTokens = entry.CompletionTokens,
            EstimatedCostUsd = pricing.EstimateCostUsd(entry.ModelName, entry.PromptTokens, entry.CompletionTokens),
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
