import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { AdjudicationService } from '../../../core/services/adjudication.service';
import { AuthService } from '../../../core/services/auth.service';
import {
  AdjudicationCase,
  AnomalyFindings,
  CoverageMatchResult,
  ExclusionAnalysisResult,
  Recommendation,
} from '../../../core/models/adjudication.model';

// A run's four stages progress in this fixed order (ADR-0009) regardless of which ones actually
// completed for a given case — used to render every stage's card, not just the ones with data.
const STAGE_ORDER = [
  'Coverage Matcher',
  'Anomaly Analyst',
  'Exclusion Analyst',
  'Adjudication Drafter',
] as const;

@Component({
  selector: 'app-run-detail',
  imports: [FormsModule],
  templateUrl: './run-detail.html',
  styleUrl: './run-detail.scss',
})
export class RunDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly adjudicationService = inject(AdjudicationService);
  private readonly destroyRef = inject(DestroyRef);
  readonly authService = inject(AuthService);

  readonly stageOrder = STAGE_ORDER;
  readonly run = signal<AdjudicationCase | null>(null);
  readonly error = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly actionInProgress = signal(false);

  readonly rejectComments = signal('');
  readonly editedRecommendationJson = signal('');
  readonly editComments = signal('');

  // Which action panel is open. Previously these were bare <details> elements, so "reject" and
  // "edit and approve" could both be expanded at once with no indication of which one the buttons
  // below belonged to -- on an irreversible decision that ambiguity is worth removing.
  readonly openAction = signal<'reject' | 'edit' | null>(null);

  readonly coverageMatch = computed<CoverageMatchResult | null>(() =>
    this.parse(this.run()?.coverageMatchResultJson),
  );
  readonly anomalyFindings = computed<AnomalyFindings | null>(() =>
    this.parse(this.run()?.anomalyFindingsJson),
  );
  readonly exclusionAnalysis = computed<ExclusionAnalysisResult | null>(() =>
    this.parse(this.run()?.exclusionAnalysisResultJson),
  );
  readonly recommendation = computed<Recommendation | null>(() =>
    this.parse(this.run()?.recommendationJson),
  );

  readonly isAwaitingApproval = computed(() => this.run()?.status === 'AwaitingApproval');

  // Which stage each pipeline status is sitting in, 1-4. FR-6 asks for live agent progress rather
  // than a frozen spinner, and every stage previously rendered the same flat "Not yet completed"
  // whether it was actively running or had not started -- so a run in progress was
  // indistinguishable from a stalled one, which is exactly the question a watching adjuster has.
  private static readonly STAGE_BY_STATUS: Record<string, number> = {
    Pending: 0,
    MatchingCoverage: 1,
    DetectingAnomalies: 2,
    AnalyzingExclusions: 3,
    Drafting: 4,
  };

  readonly isRunning = computed(() => {
    const status = this.run()?.status;
    return status !== undefined && status in RunDetail.STAGE_BY_STATUS;
  });

  readonly activeStage = computed(() => {
    const status = this.run()?.status;
    return status === undefined ? -1 : (RunDetail.STAGE_BY_STATUS[status] ?? -1);
  });

  // Status "Pending" maps to stage 0 -- the run exists but no agent has started yet, which is a
  // real state a viewer can land on and needs wording of its own rather than an empty heading.
  readonly activeStageLabel = computed<string>(() =>
    STAGE_ORDER[this.activeStage() - 1] ?? 'starting up',
  );

  /// <summary>'done' | 'active' | 'queued' for a 1-based stage number.</summary>
  stageState(stage: number): 'done' | 'active' | 'queued' {
    const active = this.activeStage();
    if (active === -1) {
      // Terminal (or awaiting approval): anything that produced output is done, the rest never ran.
      return this.hasOutput(stage) ? 'done' : 'queued';
    }

    if (stage < active || this.hasOutput(stage)) return 'done';
    return stage === active ? 'active' : 'queued';
  }

  private hasOutput(stage: number): boolean {
    switch (stage) {
      case 1: return this.coverageMatch() !== null;
      case 2: return this.anomalyFindings() !== null;
      case 3: return this.exclusionAnalysis() !== null;
      case 4: return this.recommendation() !== null;
      default: return false;
    }
  }

  // Edit-and-approve means correcting the recommendation, not retyping it: the textarea starts from
  // the agents' own output, pretty-printed, so an adjuster changes the figure they disagree with.
  readonly editedJsonError = computed<string | null>(() => {
    const raw = this.editedRecommendationJson().trim();
    if (!raw) return null;
    try {
      JSON.parse(raw);
      return null;
    } catch (err) {
      return `Not valid JSON: ${(err as Error).message}`;
    }
  });

  readonly canSubmitEdit = computed(
    () =>
      this.editedRecommendationJson().trim().length > 0 &&
      this.editComments().trim().length > 0 &&
      this.editedJsonError() === null,
  );

  openRejectPanel(): void {
    this.actionError.set(null);
    this.openAction.update((current) => (current === 'reject' ? null : 'reject'));
  }

  openEditPanel(): void {
    this.actionError.set(null);
    if (this.openAction() === 'edit') {
      this.openAction.set(null);
      return;
    }

    if (!this.editedRecommendationJson().trim()) {
      const current = this.recommendation();
      if (current) {
        this.editedRecommendationJson.set(JSON.stringify(current, null, 2));
      }
    }
    this.openAction.set('edit');
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;

    // FR-6's live agent progress: pushes an update each time the pipeline actually advances a
    // stage, and closes itself once the pipeline reaches AwaitingApproval or a terminal status
    // (see AdjudicationService.streamRun / PIPELINE_IN_PROGRESS_STATUSES) rather than polling
    // forever. takeUntilDestroyed also closes it if this view is left before that happens.
    this.adjudicationService
      .streamRun(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (run) => this.run.set(run),
        error: (err) => this.error.set(`Failed to load run: ${err.message ?? err}`),
      });
  }

  approve(): void {
    const run = this.run();
    if (!run) return;

    this.actionInProgress.set(true);
    this.actionError.set(null);
    this.adjudicationService.approve(run.id).subscribe({
      next: () => this.reloadAfterAction(run.id),
      error: (err) => this.handleActionError(err),
    });
  }

  reject(): void {
    const run = this.run();
    if (!run) return;
    if (!this.rejectComments().trim()) {
      this.actionError.set('Rejecting a run requires a reason.');
      return;
    }

    this.actionInProgress.set(true);
    this.actionError.set(null);
    this.adjudicationService
      .reject(run.id, { comments: this.rejectComments() })
      .subscribe({
        next: () => this.reloadAfterAction(run.id),
        error: (err) => this.handleActionError(err),
      });
  }

  editAndApprove(): void {
    const run = this.run();
    if (!run) return;
    if (!this.editedRecommendationJson().trim() || !this.editComments().trim()) {
      this.actionError.set(
        'Edit-and-approve requires both the edited recommendation JSON and a comment explaining the edit.',
      );
      return;
    }

    this.actionInProgress.set(true);
    this.actionError.set(null);
    this.adjudicationService
      .editAndApprove(run.id, {
        comments: this.editComments(),
        editedRecommendationJson: this.editedRecommendationJson(),
      })
      .subscribe({
        next: () => this.reloadAfterAction(run.id),
        error: (err) => this.handleActionError(err),
      });
  }

  private reloadAfterAction(id: string): void {
    this.actionInProgress.set(false);
    this.adjudicationService.getRun(id).subscribe((run) => this.run.set(run));
  }

  private handleActionError(err: { error?: string; message?: string }): void {
    this.actionInProgress.set(false);
    this.actionError.set(`Action failed: ${err.error ?? err.message ?? err}`);
  }

  private parse<T>(json: string | null | undefined): T | null {
    if (!json) return null;
    try {
      return JSON.parse(json) as T;
    } catch {
      return null;
    }
  }

  readonly memoDownloading = signal(false);

  // Fetches the PDF through HttpClient (so the auth interceptor attaches the token -- a plain
  // <a href> can't) as a Blob, then opens it from a local blob: URL. The object URL is revoked
  // after a delay rather than immediately, since the new tab needs it to still be valid when it
  // actually renders the PDF.
  downloadMemo(id: string): void {
    this.memoDownloading.set(true);
    this.actionError.set(null);
    this.adjudicationService.downloadMemo(id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        window.open(url, '_blank');
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
        this.memoDownloading.set(false);
      },
      error: (err) => {
        this.memoDownloading.set(false);
        this.actionError.set(`Failed to download memo: ${err.error ?? err.message ?? err}`);
      },
    });
  }
}
