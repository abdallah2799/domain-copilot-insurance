// Mirrors DomainCopilot.Application.Retrieval's AskRequest/AskResult/CitedChunk (camelCase, enums
// as strings — same convention as the other model files).

export interface CitedChunk {
  documentId: string;
  documentTitle: string;
  documentSourceId: string;
  sectionTitle: string;
  text: string;
  pageNumber: number | null;
  category: string;
  formVersion: string | null;
  effectiveDate: string | null;
  fusedScore: number;
  denseScore: number | null;
  keywordScore: number | null;
}

export interface AskRequest {
  question: string;
  topK?: number;
  dateOfLoss?: string | null;
  formVersion?: string | null;
  category?: string | null;
}

export interface AskResult {
  refused: boolean;
  answer: string;
  citations: string[];
  retrievedChunks: CitedChunk[];
}
