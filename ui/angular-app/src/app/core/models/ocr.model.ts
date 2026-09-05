// Mirrors DomainCopilot.Domain.Ocr.ScannedDocument (camelCase, enums as strings).

export type ScannedDocumentStatus = 'Processing' | 'Completed' | 'NeedsReview' | 'Failed';

export interface OcrPageResult {
  pageNumber: number;
  text: string;
  confidencePercent: number;
}

export interface ScannedDocument {
  id: string;
  claimNumber: string;
  sourceFileName: string;
  contentHash: string;
  status: ScannedDocumentStatus;
  pageResultsJson: string | null;
  combinedText: string | null;
  overallConfidencePercent: number | null;
  lowestPageConfidencePercent: number | null;
  errorMessage: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  processedAtUtc: string | null;
}
