// Mirrors DomainCopilot.Domain.Documents.Document and Application.Ingestion.IngestionResult
// (camelCase, enums as strings — same ASP.NET Core JSON convention as adjudication.model.ts).

export type DocumentCategory = 'PolicyForm' | 'Endorsement' | 'Reference';
export type DocumentFormat = 'Pdf' | 'Docx';
export type IngestionStatus = 'Pending' | 'Processing' | 'Completed' | 'Failed';

export interface Document {
  id: string;
  sourceId: string;
  title: string;
  category: DocumentCategory;
  format: DocumentFormat;
  sourceFileName: string;
  contentHash: string;
  formVersion: string | null;
  effectiveDate: string | null;
  status: IngestionStatus;
  errorMessage: string | null;
  chunkCount: number;
  createdAtUtc: string;
  updatedAtUtc: string;
  ingestedAtUtc: string | null;
}

export interface IngestionResult {
  sourceId: string;
  status: IngestionStatus;
  chunkCount: number;
  errorMessage: string | null;
}
