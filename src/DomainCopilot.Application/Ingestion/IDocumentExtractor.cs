using DomainCopilot.Domain.Documents;

namespace DomainCopilot.Application.Ingestion;

/// <summary>
/// Port over format-specific extraction (PdfPig for PDF, OpenXml for DOCX in Infrastructure).
/// Application never references either SDK directly — it only knows it can turn bytes + a format
/// into structured sections.
/// </summary>
public interface IDocumentExtractor
{
    Task<ExtractedDocument> ExtractAsync(Stream content, DocumentFormat format, CancellationToken cancellationToken = default);
}
