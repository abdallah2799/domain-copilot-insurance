import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { firstValueFrom, toArray } from 'rxjs';
import { vi } from 'vitest';

import { AdjudicationService } from './adjudication.service';
import { AdjudicationCase } from '../models/adjudication.model';

function sseBytes(...lines: string[]): Uint8Array {
  return new TextEncoder().encode(lines.join(''));
}

// See RetrievalService.askStream's own spec for why this fakes only `reader.read()`'s contract
// rather than a real ReadableStream (inconsistent across the Angular test builder's environments).
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

function fakeCase(overrides: Partial<AdjudicationCase>): AdjudicationCase {
  return {
    id: 'case-1',
    claimNumber: 'CLM-1',
    policyNumber: 'POL-1',
    dateOfLoss: '2025-08-03',
    status: 'MatchingCoverage',
    coverageMatchResultJson: null,
    anomalyFindingsJson: null,
    exclusionAnalysisResultJson: null,
    recommendationJson: null,
    approvedBy: null,
    approvedAtUtc: null,
    adjusterComments: null,
    failureReason: null,
    createdByUsername: 'analyst',
    createdAtUtc: '2026-09-05T00:00:00Z',
    updatedAtUtc: '2026-09-05T00:00:00Z',
    ...overrides,
  };
}

function updateEvent(run: AdjudicationCase): Uint8Array {
  return sseBytes('event: update\n', `data: ${JSON.stringify(run)}\n\n`);
}

describe('AdjudicationService.streamRun', () => {
  let service: AdjudicationService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AdjudicationService);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('emits each update and completes once the pipeline reaches a terminal status', async () => {
    const reader = fakeReader([
      updateEvent(fakeCase({ status: 'MatchingCoverage' })),
      updateEvent(fakeCase({ status: 'DetectingAnomalies' })),
      updateEvent(fakeCase({ status: 'Failed', failureReason: 'boom' })),
    ]);
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({ ok: true, status: 200, body: { getReader: () => reader } }),
    );

    const emitted = await firstValueFrom(service.streamRun('case-1').pipe(toArray<AdjudicationCase>()));

    expect(emitted.map((c) => c.status)).toEqual(['MatchingCoverage', 'DetectingAnomalies', 'Failed']);
  });

  it('closes itself at AwaitingApproval too, even though the case is not yet terminal', async () => {
    const reader = fakeReader([updateEvent(fakeCase({ status: 'AwaitingApproval' }))]);
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({ ok: true, status: 200, body: { getReader: () => reader } }),
    );

    const emitted = await firstValueFrom(service.streamRun('case-1').pipe(toArray<AdjudicationCase>()));

    expect(emitted.map((c) => c.status)).toEqual(['AwaitingApproval']);
  });

  it('surfaces an HTTP failure rather than silently completing', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false, status: 401, body: null }));

    let error: unknown;
    service.streamRun('case-1').subscribe({ error: (err) => (error = err) });
    await Promise.resolve();
    await Promise.resolve();

    expect(error).toBeInstanceOf(Error);
  });

  it('aborts the underlying fetch when unsubscribed -- real cancellation, not just stopping local updates', async () => {
    let capturedSignal: AbortSignal | undefined;
    const neverResolvingReader = { read: vi.fn(() => new Promise(() => {})) };
    const fetchMock = vi.fn((_url: string, init: RequestInit) => {
      capturedSignal = init.signal as AbortSignal;
      return Promise.resolve({ ok: true, status: 200, body: { getReader: () => neverResolvingReader } });
    });
    vi.stubGlobal('fetch', fetchMock);

    const subscription = service.streamRun('case-1').subscribe();
    await Promise.resolve();

    expect(capturedSignal?.aborted).toBe(false);
    subscription.unsubscribe();
    expect(capturedSignal?.aborted).toBe(true);
  });
});

describe('AdjudicationService.downloadMemo', () => {
  let service: AdjudicationService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AdjudicationService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  // Regression test: this used to be a plain URL opened in an <a href>, which 401'd once FR-8
  // made this endpoint require a bearer token -- a browser navigating straight to a URL can't
  // attach an Authorization header, only HttpClient (via the auth interceptor) can. Fetching it
  // through HttpClient as a blob is what actually fixes that.
  it('requests the memo as a blob through HttpClient, not a raw URL', () => {
    const fakePdf = new Blob(['%PDF-fake'], { type: 'application/pdf' });

    service.downloadMemo('case-1').subscribe((blob) => {
      expect(blob).toBe(fakePdf);
    });

    const req = httpMock.expectOne('http://localhost:5080/api/adjudication/runs/case-1/memo');
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
    req.flush(fakePdf);
  });
});

describe('AdjudicationService.extractIntakeFields', () => {
  let service: AdjudicationService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AdjudicationService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('posts the file as multipart form data and returns the extracted fields', () => {
    const file = new File(['%PDF-fake'], 'intake.pdf', { type: 'application/pdf' });
    const response = {
      combinedText: 'Claim Number: CLM-1',
      overallConfidencePercent: 95.35,
      fields: {
        claimNumber: 'CLM-1',
        policyNumber: 'POL-1',
        dateOfLoss: '2025-08-03',
        policeReportNumber: 'CPD-1',
      },
    };

    service.extractIntakeFields(file).subscribe((result) => {
      expect(result).toEqual(response);
    });

    const req = httpMock.expectOne('http://localhost:5080/api/adjudication/runs/extract-intake-fields');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toBeInstanceOf(FormData);
    expect((req.request.body as FormData).get('file')).toBe(file);
    req.flush(response);
  });
});
