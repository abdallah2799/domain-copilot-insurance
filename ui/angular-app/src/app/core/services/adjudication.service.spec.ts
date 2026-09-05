import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { vi } from 'vitest';

import { AdjudicationService } from './adjudication.service';
import { AdjudicationCase } from '../models/adjudication.model';

// jsdom (this test environment) doesn't implement EventSource -- a small fake standing in for the
// one piece AdjudicationService.streamRun actually uses (addEventListener('update', ...), onerror,
// close()) is simpler and more reliable across environments than trying to polyfill the real thing.
class FakeEventSource {
  static instances: FakeEventSource[] = [];

  url: string;
  closed = false;
  onerror: (() => void) | null = null;
  private listeners: Record<string, ((event: { data: string }) => void)[]> = {};

  constructor(url: string) {
    this.url = url;
    FakeEventSource.instances.push(this);
  }

  addEventListener(type: string, callback: (event: { data: string }) => void): void {
    (this.listeners[type] ??= []).push(callback);
  }

  close(): void {
    this.closed = true;
  }

  emit(type: string, data: unknown): void {
    for (const callback of this.listeners[type] ?? []) {
      callback({ data: JSON.stringify(data) });
    }
  }
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
    createdAtUtc: '2026-09-05T00:00:00Z',
    updatedAtUtc: '2026-09-05T00:00:00Z',
    ...overrides,
  };
}

describe('AdjudicationService.streamRun', () => {
  let service: AdjudicationService;

  beforeEach(() => {
    FakeEventSource.instances = [];
    vi.stubGlobal('EventSource', FakeEventSource);
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AdjudicationService);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('emits each update and completes once the pipeline reaches a terminal status', () => {
    const emitted: AdjudicationCase[] = [];
    let completed = false;

    service.streamRun('case-1').subscribe({
      next: (run) => emitted.push(run),
      complete: () => (completed = true),
    });

    const source = FakeEventSource.instances[0];
    source.emit('update', fakeCase({ status: 'MatchingCoverage' }));
    source.emit('update', fakeCase({ status: 'DetectingAnomalies' }));
    source.emit('update', fakeCase({ status: 'Failed', failureReason: 'boom' }));

    expect(emitted.map((c) => c.status)).toEqual([
      'MatchingCoverage',
      'DetectingAnomalies',
      'Failed',
    ]);
    expect(completed).toBe(true);
    expect(source.closed).toBe(true);
  });

  it('closes itself at AwaitingApproval too, even though the case is not yet terminal', () => {
    let completed = false;
    service.streamRun('case-1').subscribe({ complete: () => (completed = true) });

    FakeEventSource.instances[0].emit('update', fakeCase({ status: 'AwaitingApproval' }));

    expect(completed).toBe(true);
    expect(FakeEventSource.instances[0].closed).toBe(true);
  });

  it('surfaces a genuine connection error rather than retrying silently', () => {
    let error: unknown;
    service.streamRun('case-1').subscribe({ error: (err) => (error = err) });

    FakeEventSource.instances[0].onerror?.();

    expect(error).toBeInstanceOf(Error);
    expect(FakeEventSource.instances[0].closed).toBe(true);
  });

  it('closes the EventSource on unsubscribe before any terminal update arrives', () => {
    const subscription = service.streamRun('case-1').subscribe();
    const source = FakeEventSource.instances[0];

    expect(source.closed).toBe(false);
    subscription.unsubscribe();
    expect(source.closed).toBe(true);
  });
});
