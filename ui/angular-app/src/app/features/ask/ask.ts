import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RetrievalService } from '../../core/services/retrieval.service';
import { AskResult } from '../../core/models/retrieval.model';

@Component({
  selector: 'app-ask',
  imports: [FormsModule],
  templateUrl: './ask.html',
  styleUrl: './ask.scss',
})
export class Ask {
  private readonly retrievalService = inject(RetrievalService);

  readonly question = signal('');
  readonly result = signal<AskResult | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  submit(): void {
    const question = this.question().trim();
    if (!question) return;

    this.loading.set(true);
    this.error.set(null);
    this.result.set(null);

    this.retrievalService.ask({ question }).subscribe({
      next: (result) => {
        this.result.set(result);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(`Ask failed: ${err.error ?? err.message ?? err}`);
        this.loading.set(false);
      },
    });
  }
}
