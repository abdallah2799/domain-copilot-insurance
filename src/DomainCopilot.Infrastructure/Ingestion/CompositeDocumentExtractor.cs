using DomainCopilot.Application.Ingestion;
using KnowledgeDocumentFormat = DomainCopilot.Domain.Documents.DocumentFormat;

namespace DomainCopilot.Infrastructure.Ingestion;

public sealed class CompositeDocumentExtractor(PdfKnowledgeExtractor pdfExtractor, DocxKnowledgeExtractor docxExtractor) : IDocumentExtractor
{
    public Task<ExtractedDocument> ExtractAsync(Stream content, KnowledgeDocumentFormat format, CancellationToken cancellationToken = default) => format switch
    {
        KnowledgeDocumentFormat.Pdf => pdfExtractor.ExtractAsync(content, cancellationToken),
        KnowledgeDocumentFormat.Docx => docxExtractor.ExtractAsync(content, cancellationToken),
        _ => throw new NotSupportedException($"No extractor registered for format {format}."),
    };
}
