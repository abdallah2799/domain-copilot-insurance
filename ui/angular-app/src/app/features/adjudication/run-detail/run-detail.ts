import { Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { interval, startWith, switchMap } from 'rxjs';
import { AdjudicationService } from '../../../core/services/adjudication.service';
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

const TERMINAL_STATUSES = new Set(['Approved', 'Rejected', 'EditedAndApproved', 'Failed']);

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

  readonly stageOrder = STAGE_ORDER;
  readonly run = signal<AdjudicationCase | null>(null);
  readonly error = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly actionInProgress = signal(false);

  readonly actorName = signal('adjuster@meridianmutual.example');
  readonly rejectComments = signal('');
  readonly editedRecommendationJson = signal('');
  readonly editComments = signal('');

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

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;

    // Poll every 3s so a run opened from run-list while another client's POST is still mid-pipeline
    // shows live progress; harmless once a run reaches a terminal status since it's just a cheap
    // GET returning the same data each time. Stops automatically when the component is destroyed.
    interval(3000)
      .pipe(
        startWith(0),
        switchMap(() => this.adjudicationService.getRun(id)),
        takeUntilDestroyed(this.destroyRef),
      )
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
    this.adjudicationService.approve(run.id, { actor: this.actorName() }).subscribe({
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
      .reject(run.id, { actor: this.actorName(), comments: this.rejectComments() })
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
        actor: this.actorName(),
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

  isTerminal(run: AdjudicationCase): boolean {
    return TERMINAL_STATUSES.has(run.status);
  }
}
