namespace DomainCopilot.Application.Observability;

public interface ITokenUsageQueryService
{
    Task<TokenUsageReport> GetReportAsync(int recentLimit = 100, CancellationToken cancellationToken = default);
}
