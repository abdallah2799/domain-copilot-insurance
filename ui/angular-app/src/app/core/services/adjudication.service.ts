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
import { AuthService } from './auth.service';

// No environment.ts (this scaffold wasn't generated with --environments) — a single dev-time
// constant is enough for now; revisit if/when a real deployment target needs a different API host.
const API_BASE_URL = 'http://localhost:5080';

@Injectable({ providedIn: 'root' })
export class AdjudicationService {
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService);
  private readonly baseUrl = `${API_BASE_URL}/api/adjudication`;

  listRuns(): Observable<AdjudicationCase[]> {
    return this.http.get<AdjudicationCase[]>(`${this.baseUrl}/runs`);
  }

  getRun(id: string): Observable<AdjudicationCase> {
    return this.http.get<AdjudicationCase>(`${this.baseUrl}/runs/${id}`);
  }

  // T6's document-out half (ADR-0011): a plain URL, not an HttpClient call -- the endpoint
  // returns a real PDF file, and a browser handles a direct GET to a file URL (download/open in a
  // new tab) natively, no fetch/blob handling needed.
  memoUrl(id: string): string {
    return `${this.baseUrl}/runs/${id}/memo`;
  }

  // The API now creates the case and returns as soon as it exists (FR-6: "watch it start
  // immediately"), running the four-agent pipeline in the background rather than making this call
  // wait for the whole thing -- watch actual progress via streamRun, not by waiting on this.
  startRun(request: StartAdjudicationRequest): Observable<AdjudicationCase> {
    return this.http.post<AdjudicationCase>(`${this.baseUrl}/runs`, request);
  }

  // Used to be a plain GET read via the browser's native EventSource -- but EventSource has no way
  // to attach a header, and FR-8 requires a bearer token on this endpoint like every other one now,
  // so this reads the same SSE response via fetch instead (the same reason, and the same approach,
  // as RetrievalService.askStream already uses for its POST). Real cancellation for free: the
  // server sees the aborted fetch as its own CancellationToken firing (AdjudicationController.
  // GetRunStream), stopping its poll loop rather than just abandoning the response client-side.
  streamRun(id: string): Observable<AdjudicationCase> {
    return new Observable<AdjudicationCase>((subscriber) => {
      const controller = new AbortController();

      (async () => {
        try {
          const response = await fetch(`${this.baseUrl}/runs/${id}/stream`, {
            headers: this.authService.token ? { Authorization: `Bearer ${this.authService.token}` } : {},
            signal: controller.signal,
          });

          if (!response.ok || !response.body) {
            subscriber.error(new Error(`Run progress stream failed: HTTP ${response.status}`));
            return;
          }

          const reader = response.body.getReader();
          const decoder = new TextDecoder();
          let buffer = '';

          while (true) {
            const { done, value } = await reader.read();
            if (done) break;

            buffer += decoder.decode(value, { stream: true });
            const events = buffer.split('\n\n');
            buffer = events.pop() ?? '';

            for (const rawEvent of events) {
              const adjudicationCase = parseUpdateEvent(rawEvent);
              if (!adjudicationCase) continue;

              subscriber.next(adjudicationCase);
              if (!PIPELINE_IN_PROGRESS_STATUSES.has(adjudicationCase.status)) {
                controller.abort();
                subscriber.complete();
                return;
              }
            }
          }

          subscriber.complete();
        } catch (err) {
          if ((err as { name?: string })?.name !== 'AbortError') {
            subscriber.error(err);
          }
        }
      })();

      return () => controller.abort();
    });
  }

  approve(id: string): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/runs/${id}/approve`, {});
  }

  reject(id: string, request: ApprovalRequest): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/runs/${id}/reject`, request);
  }

  editAndApprove(id: string, request: EditAndApproveRequest): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/runs/${id}/edit-and-approve`, request);
  }
}

// Only the "update" event carries a payload worth parsing here -- a keep-alive is a bare `:
// keep-alive\n\n` comment line with no `event:`/`data:` pair, so it naturally parses to null below.
function parseUpdateEvent(rawEvent: string): AdjudicationCase | null {
  let eventName: string | null = null;
  let data: string | null = null;

  for (const line of rawEvent.split('\n')) {
    if (line.startsWith('event: ')) {
      eventName = line.slice('event: '.length);
    } else if (line.startsWith('data: ')) {
      data = line.slice('data: '.length);
    }
  }

  return eventName === 'update' && data !== null ? (JSON.parse(data) as AdjudicationCase) : null;
}
