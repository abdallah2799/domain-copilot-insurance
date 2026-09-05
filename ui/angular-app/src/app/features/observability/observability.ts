import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ObservabilityService } from '../../core/services/observability.service';
import { TokenUsageReport } from '../../core/models/observability.model';

@Component({
  selector: 'app-observability',
  imports: [DatePipe, DecimalPipe],
  templateUrl: './observability.html',
  styleUrl: './observability.scss',
})
export class Observability implements OnInit {
  private readonly observabilityService = inject(ObservabilityService);

  readonly report = signal<TokenUsageReport | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.error.set(null);
    this.observabilityService.getTokenUsage().subscribe({
      next: (report) => {
        this.report.set(report);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(`Failed to load token usage: ${err.message ?? err}`);
        this.loading.set(false);
      },
    });
  }
}
