import { DecimalPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AdjudicationService } from '../../../core/services/adjudication.service';

@Component({
  selector: 'app-start-run',
  imports: [ReactiveFormsModule, DecimalPipe],
  templateUrl: './start-run.html',
  styleUrl: './start-run.scss',
})
export class StartRun {
  private readonly fb = inject(FormBuilder);
  private readonly adjudicationService = inject(AdjudicationService);
  private readonly router = inject(Router);

  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);

  // T6: the adjuster uploads the claim's own intake-form PDF instead of retyping claim/policy
  // number and date of loss -- see AdjudicationService.extractIntakeFields. Extraction only ever
  // pre-fills the fields below; it never auto-submits, since a misread field (bad OCR, an unusual
  // form layout) must stay something the adjuster can see and correct before a run starts.
  readonly extracting = signal(false);
  readonly extractionError = signal<string | null>(null);
  readonly extractionConfidencePercent = signal<number | null>(null);
  readonly extractedPoliceReportNumber = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    claimNumber: ['', Validators.required],
    policyNumber: ['', Validators.required],
    dateOfLoss: ['', Validators.required],
    lossType: ['Collision', Validators.required],
    narrative: ['', Validators.required],
    policeReportText: [''],
    estimatedDamage: [0, [Validators.required, Validators.min(0)]],
    approximateVehicleValue: [0, [Validators.required, Validators.min(0.01)]],
  });

  onIntakeFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    this.extracting.set(true);
    this.extractionError.set(null);
    this.extractedPoliceReportNumber.set(null);

    this.adjudicationService.extractIntakeFields(file).subscribe({
      next: (result) => {
        this.extracting.set(false);
        this.extractionConfidencePercent.set(result.overallConfidencePercent);
        this.extractedPoliceReportNumber.set(result.fields.policeReportNumber);

        const { claimNumber, policyNumber, dateOfLoss } = result.fields;
        this.form.patchValue({
          ...(claimNumber ? { claimNumber } : {}),
          ...(policyNumber ? { policyNumber } : {}),
          ...(dateOfLoss ? { dateOfLoss } : {}),
        });
      },
      error: (err) => {
        this.extracting.set(false);
        this.extractionError.set(`Could not read the intake form: ${err.error ?? err.message ?? err}`);
      },
    });

    // Allow re-selecting the same file (e.g. after fixing a scan) to fire another change event.
    input.value = '';
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.submitting.set(true);
    this.error.set(null);

    this.adjudicationService
      .startRun({
        ...value,
        policeReportText: value.policeReportText.trim().length > 0 ? value.policeReportText : null,
      })
      .subscribe({
        // The API creates the case and returns immediately, running the four-agent pipeline in
        // the background (FR-6) -- this call itself resolves in well under a second; the run
        // detail page picks up live progress from there via AdjudicationService.streamRun.
        next: (result) => {
          this.submitting.set(false);
          this.router.navigate(['/runs', result.id]);
        },
        error: (err) => {
          this.submitting.set(false);
          this.error.set(`Failed to start run: ${err.error ?? err.message ?? err}`);
        },
      });
  }
}
