import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AskRequest, AskResult, AskStreamEvent } from '../models/retrieval.model';

const API_BASE_URL = 'http://localhost:5080';

@Injectable({ providedIn: 'root' })
export class RetrievalService {
  private readonly http = inject(HttpClient);

  // A refused question returns immediately (no LLM call, per AskService); a grounded one makes one
  // real completion call, so this is a fraction of a second to tens of seconds depending on the
  // provider, not the minutes a full adjudication run takes.
  ask(request: AskRequest): Observable<AskResult> {
    return this.http.post<AskResult>(`${API_BASE_URL}/api/retrieval/ask`, request);
  }

  // Browser EventSource doesn't support POST (or a request body), so this reads the SSE response
  // via fetch's own ReadableStream instead -- which also gives real cancellation for free:
  // unsubscribing runs this Observable's teardown, which aborts the fetch, which the server sees
  // as its CancellationToken firing (RetrievalController.AskStream), stopping the real completion
  // call server-side rather than just abandoning the response client-side.
  askStream(request: AskRequest): Observable<AskStreamEvent> {
    return new Observable<AskStreamEvent>((subscriber) => {
      const controller = new AbortController();

      (async () => {
        try {
          const response = await fetch(`${API_BASE_URL}/api/retrieval/ask/stream`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(request),
            signal: controller.signal,
          });

          if (!response.ok || !response.body) {
            subscriber.error(new Error(`Ask stream failed: HTTP ${response.status}`));
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
              const parsed = parseSseEvent(rawEvent);
              if (parsed) {
                subscriber.next(parsed);
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
}

function parseSseEvent(rawEvent: string): AskStreamEvent | null {
  let eventName: string | null = null;
  let data: string | null = null;

  for (const line of rawEvent.split('\n')) {
    if (line.startsWith('event: ')) {
      eventName = line.slice('event: '.length);
    } else if (line.startsWith('data: ')) {
      data = line.slice('data: '.length);
    }
  }

  if (!eventName || data === null) return null;
  const payload = JSON.parse(data);

  switch (eventName) {
    case 'refused':
      return { type: 'refused', message: payload.message, chunks: payload.chunks ?? [] };
    case 'delta':
      return { type: 'delta', text: payload.text };
    case 'done':
      return { type: 'done', citations: payload.citations ?? [], chunks: payload.chunks ?? [] };
    default:
      return null;
  }
}
