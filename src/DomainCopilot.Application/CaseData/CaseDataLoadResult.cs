namespace DomainCopilot.Application.CaseData;

public sealed record CaseDataLoadResult(int DeclarationsLoaded, int DeclarationsSkipped, int ClaimsLoaded, int ClaimsSkipped);
