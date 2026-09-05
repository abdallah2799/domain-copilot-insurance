using DomainCopilot.Application.Observability;
using Microsoft.AspNetCore.Mvc;

namespace DomainCopilot.Api.Controllers;

/// <summary>FR-9's persisted per-request token/cost accounting, queryable rather than only visible
/// in the trace viewer. Any authenticated user (the default fallback policy) can read this --
/// there's no ownership concept for aggregate usage the way there is for adjudication runs.</summary>
[ApiController]
[Route("api/observability")]
public sealed class ObservabilityController(ITokenUsageQueryService queryService) : ControllerBase
{
    [HttpGet("token-usage")]
    public async Task<ActionResult<TokenUsageReport>> GetTokenUsage([FromQuery] int recentLimit, CancellationToken cancellationToken) =>
        Ok(await queryService.GetReportAsync(recentLimit <= 0 ? 100 : recentLimit, cancellationToken));
}
