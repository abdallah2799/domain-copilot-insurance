import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom, toArray } from 'rxjs';
import { vi } from 'vitest';

import { RetrievalService } from './retrieval.service';
import { AskStreamEvent } from '../models/retrieval.model';

function sseBytes(...lines: string[]): Uint8Array {
  return new TextEncoder().encode(lines.join(''));
}

// A fake `Response.body.getReader()` that yields each provided chunk in order, then signals done --
// deliberately not a real ReadableStream (Node's global one behaves inconsistently across the
// Angular test builder's environments); the only contract RetrievalService.askStream actually
// relies on is `{ done, value }` from `reader.read()`, so faking exactly that is both simpler and
// more portable than constructing a real stream.
function fakeReader(chunks: Uint8Array[]) {
  let index = 0;
  return {
    read: vi.fn(async () => {
      if (index < chunks.length) {
        return { done: false, value: chunks[index++] };
      }
      return { done: true, value: undefined };
    }),
  };
}

describe('RetrievalService.askStream', () => {
  let service: RetrievalService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(RetrievalService);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('parses delta events in order followed by a done event with citations', async () => {
    const reader = fakeReader([
      sseBytes('event: delta\n', 'data: {"text":"No, "}\n\n'),
      sseBytes('event: delta\n', 'data: {"text":"it does not."}\n\n'),
      sseBytes(': keep-alive\n\n'),
      sseBytes('event: done\n', 'data: {"citations":["Some Doc, p.1"],"chunks":[]}\n\n'),
    ]);
    const fetchMock = vi
      .fn()
      .mockResolvedValue({ ok: true, status: 200, body: { getReader: () => reader } });
    vi.stubGlobal('fetch', fetchMock);

    const events = await firstValueFrom(
      service.askStream({ question: 'does the waiver apply?' }).pipe(toArray<AskStreamEvent>()),
    );

    expect(events).toEqual([
      { type: 'delta', text: 'No, ' },
      { type: 'delta', text: 'it does not.' },
      { type: 'done', citations: ['Some Doc, p.1'], chunks: [] },
    ]);
    // The keep-alive comment line has no `event:`/`data:` pair, so it's correctly ignored rather
    // than surfacing as a fourth, malformed event.

    const [, init] = fetchMock.mock.calls[0];
    expect(init.method).toBe('POST');
    expect(JSON.parse(init.body)).toEqual({ question: 'does the waiver apply?' });
  });

  it('yields a single refused event and stops, without a done event', async () => {
    const reader = fakeReader([
      sseBytes('event: refused\n', 'data: {"message":"no evidence","chunks":[]}\n\n'),
    ]);
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({ ok: true, status: 200, body: { getReader: () => reader } }),
    );

    const events = await firstValueFrom(
      service.askStream({ question: 'unrelated question' }).pipe(toArray<AskStreamEvent>()),
    );

    expect(events).toEqual([{ type: 'refused', message: 'no evidence', chunks: [] }]);
  });

  it('aborts the underlying fetch when unsubscribed -- real cancellation, not just stopping local updates', async () => {
    let capturedSignal: AbortSignal | undefined;
    const neverResolvingReader = { read: vi.fn(() => new Promise(() => {})) };
    const fetchMock = vi.fn((_url: string, init: RequestInit) => {
      capturedSignal = init.signal as AbortSignal;
      return Promise.resolve({
        ok: true,
        status: 200,
        body: { getReader: () => neverResolvingReader },
      });
    });
    vi.stubGlobal('fetch', fetchMock);

    const subscription = service.askStream({ question: 'anything' }).subscribe();
    await Promise.resolve(); // let the async IIFE inside askStream reach the fetch call

    expect(capturedSignal?.aborted).toBe(false);
    subscription.unsubscribe();
    expect(capturedSignal?.aborted).toBe(true);
  });
});
