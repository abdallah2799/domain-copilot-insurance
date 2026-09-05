namespace DomainCopilot.Application.Ocr;

public sealed record OcrIngestionRequest(string ClaimNumber, string SourceFileName, byte[] PdfContent);
