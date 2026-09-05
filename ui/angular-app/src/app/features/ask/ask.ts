import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { RetrievalService } from '../../core/services/retrieval.service';
import { CitedChunk } from '../../core/models/retrieval.model';

@Component({
  selector: 'app-ask',
  imports: [FormsModule],
  templateUrl: './ask.html',
  styleUrl: './ask.scss',
})
export class Ask {
  private readonly retrievalService = inject(RetrievalService);
  private subscription: Subscription | null = null;

  readonly question = signal('');
  readonly streaming = signal(false);
  readonly refused = signal(false);
  readonly answer = signal('');
  readonly citations = signal<string[]>([]);
  readonly chunks = signal<CitedChunk[]>([]);
  readonly error = signal<string | null>(null);

  submit(): void {
    const question = this.question().trim();
    if (!question) return;

    this.streaming.set(true);
    this.error.set(null);
    this.refused.set(false);
    this.answer.set('');
    this.citations.set([]);
    this.chunks.set([]);

    this.subscription = this.retrievalService.askStream({ question }).subscribe({
      next: (event) => {
        switch (event.type) {
          case 'refused':
            this.refused.set(true);
            this.answer.set(event.message);
            this.chunks.set(event.chunks);
            this.streaming.set(false);
            break;
          case 'delta':
            this.answer.update((current) => current + event.text);
            break;
          case 'done':
            this.citations.set(event.citations);
            this.chunks.set(event.chunks);
            this.streaming.set(false);
            break;
        }
      },
      error: (err) => {
        this.error.set(`Ask failed: ${err.message ?? err}`);
        this.streaming.set(false);
      },
    });
  }

  // Unsubscribing runs RetrievalService.askStream's teardown, which aborts the underlying fetch --
  // a real cancellation that stops the server-side completion call too (see the service and
  // RetrievalController.AskStream doc comments), not just a client-side "stop showing updates."
  cancel(): void {
    this.subscription?.unsubscribe();
    this.subscription = null;
    this.streaming.set(false);
  }
}
