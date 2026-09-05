using DomainCopilot.Application.Ocr;
using DomainCopilot.Domain.Ocr;
using Microsoft.AspNetCore.Mvc;

namespace DomainCopilot.Api.Controllers;

/// <summary>
/// T6's document-in surface: upload a scanned claim document (a claim intake form, a police
/// report scan, ...), run it through OCR, and get back real per-page confidence -- a page below
/// <see cref="ScannedDocument.ConfidenceThresholdPercent"/> routes the whole document to
/// <see cref="ScannedDocumentStatus.NeedsReview"/> rather than being silently trusted (ADR-0010).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class OcrController(OcrIngestionService ocrIngestionService, IScannedDocumentRepository repository) : ControllerBase
{
    private const long MaxUploadBytes = 20 * 1024 * 1024;

    [HttpPost("documents")]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<ActionResult<ScannedDocument>> UploadDocument(
        [FromForm] string claimNumber, [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(claimNumber))
        {
            return BadRequest("claimNumber is required.");
        }

        if (file.Length == 0)
        {
            return BadRequest("file is required and must not be empty.");
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);

        var result = await ocrIngestionService.ProcessAsync(
            new OcrIngestionRequest(claimNumber, file.FileName, stream.ToArray()), cancellationToken);

        return Ok(result);
    }

    [HttpGet("documents/{id:guid}")]
    public async Task<ActionResult<ScannedDocument>> GetDocument(Guid id, CancellationToken cancellationToken)
    {
        var document = await repository.FindByIdAsync(id, cancellationToken);
        return document is null ? NotFound() : Ok(document);
    }

    [HttpGet("documents")]
    public async Task<ActionResult<IReadOnlyList<ScannedDocument>>> ListDocuments(
        [FromQuery] string claimNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(claimNumber))
        {
            return BadRequest("claimNumber query parameter is required.");
        }

        return Ok(await repository.ListByClaimNumberAsync(claimNumber, cancellationToken));
    }
}
