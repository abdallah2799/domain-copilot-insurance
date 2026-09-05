import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AdjudicationCase,
  ApprovalRequest,
  EditAndApproveRequest,
  PIPELINE_IN_PROGRESS_STATUSES,
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

  // The API now creates the case and returns as soon as it exists (FR-6: "watch it start
  // immediately"), running the four-agent pipeline in the background rather than making this call
  // wait for the whole thing -- watch actual progress via streamRun, not by waiting on this.
  startRun(request: StartAdjudicationRequest): Observable<AdjudicationCase> {
    return this.http.post<AdjudicationCase>(`${this.baseUrl}/runs`, request);
  }

  // A GET with no request body, so the browser's native EventSource works fine here (unlike
  // RetrievalService.askStream's POST, which needs fetch instead). EventSource auto-reconnects by
  // design when a stream ends -- since the server intentionally ends the response once the run
  // reaches a terminal status, this closes the connection itself right after that update rather
  // than letting EventSource treat "the server finished" as "the connection dropped, retry."
  streamRun(id: string): Observable<AdjudicationCase> {
    return new Observable<AdjudicationCase>((subscriber) => {
      const eventSource = new EventSource(`${this.baseUrl}/runs/${id}/stream`);

      eventSource.addEventListener('update', (event) => {
        const adjudicationCase = JSON.parse((event as MessageEvent).data) as AdjudicationCase;
        subscriber.next(adjudicationCase);

        if (!PIPELINE_IN_PROGRESS_STATUSES.has(adjudicationCase.status)) {
          eventSource.close();
          subscriber.complete();
        }
      });

      eventSource.onerror = () => {
        // A genuine connection failure (not the intentional close() above, which unsubscribes
        // before this can fire) -- surface it rather than silently retrying forever.
        eventSource.close();
        subscriber.error(new Error('Run progress stream disconnected.'));
      };

      return () => eventSource.close();
    });
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
