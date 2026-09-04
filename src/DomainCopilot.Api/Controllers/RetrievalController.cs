using DomainCopilot.Application.Retrieval;
using DomainCopilot.Domain.Documents;
using Microsoft.AspNetCore.Mvc;

namespace DomainCopilot.Api.Controllers;

/// <summary>
/// FR-2's retrieval surface: hybrid dense+keyword search over the knowledge corpus, with
/// version/date-aware filtering (ADR-0005) and a structured refusal signal for low-evidence
/// queries. Returns citations, not a synthesized answer — answer generation is a later, separate
/// concern (the agentic workflow, FR-4/FR-5) built on top of this.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class RetrievalController(HybridRetrievalService retrievalService) : ControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<RetrievalResult>> Search(
        [FromQuery] string query,
        [FromQuery] int topK = 5,
        [FromQuery] DateOnly? dateOfLoss = null,
        [FromQuery] string? formVersion = null,
        [FromQuery] DocumentCategory? category = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("query is required.");
        }

        var result = await retrievalService.SearchAsync(
            new RetrievalQuery(query, topK, dateOfLoss, formVersion, category),
            cancellationToken);

        return Ok(result);
    }
}
