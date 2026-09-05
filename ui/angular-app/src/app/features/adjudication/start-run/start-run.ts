import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AdjudicationService } from '../../../core/services/adjudication.service';

@Component({
  selector: 'app-start-run',
  imports: [ReactiveFormsModule],
  templateUrl: './start-run.html',
  styleUrl: './start-run.scss',
})
export class StartRun {
  private readonly fb = inject(FormBuilder);
  private readonly adjudicationService = inject(AdjudicationService);
  private readonly router = inject(Router);

  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);

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
        // The orchestrator runs the full pipeline synchronously before responding, so this can
        // take anywhere from under a minute to several minutes depending on the completion
        // provider — the "submitting" state stays true for the whole call, deliberately, rather
        // than optimistically navigating away before a result actually exists.
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
