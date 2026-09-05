import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Document, IngestionResult } from '../models/document.model';

const API_BASE_URL = 'http://localhost:5080';

@Injectable({ providedIn: 'root' })
export class DocumentService {
  private readonly http = inject(HttpClient);

  listDocuments(): Observable<Document[]> {
    return this.http.get<Document[]>(`${API_BASE_URL}/api/documents`);
  }

  // Walks the corpus manifest server-side (default path from Ingestion:CorpusPath config) --
  // idempotent on content hash, so calling this again on an unchanged corpus is a cheap no-op per
  // document rather than a full re-ingest.
  ingestKnowledgeCorpus(): Observable<IngestionResult[]> {
    return this.http.post<IngestionResult[]>(`${API_BASE_URL}/api/ingestion/knowledge-corpus`, {});
  }
}
