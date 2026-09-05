import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AdjudicationCase,
  ApprovalRequest,
  EditAndApproveRequest,
  StartAdjudicationRequest,
} from '../models/adjudication.model';

// No environment.ts (this scaffold wasn't generated with --environments) — a single dev-time
// constant is enough for now; revisit if/when a real deployment target needs a different API host.
const API_BASE_URL = 'http://localhost:5080';

@Injectable({ providedIn: 'root' })
export class AdjudicationService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${API_BASE_URL}/api/adjudication`;

  listRuns(): Observable<AdjudicationCase[]> {
    return this.http.get<AdjudicationCase[]>(`${this.baseUrl}/runs`);
  }

  getRun(id: string): Observable<AdjudicationCase> {
    return this.http.get<AdjudicationCase>(`${this.baseUrl}/runs/${id}`);
  }

  // A full run (all four agent stages, real LLM calls) can take from under a minute (hosted) to
  // several minutes (local Ollama) — matches the orchestrator's own per-step timeout budget rather
  // than the browser's/HttpClient's default.
  startRun(request: StartAdjudicationRequest): Observable<AdjudicationCase> {
    return this.http.post<AdjudicationCase>(`${this.baseUrl}/runs`, request);
  }

  approve(id: string, request: ApprovalRequest): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/runs/${id}/approve`, request);
  }

  reject(id: string, request: ApprovalRequest): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/runs/${id}/reject`, request);
  }

  editAndApprove(id: string, request: EditAndApproveRequest): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/runs/${id}/edit-and-approve`, request);
  }
}
