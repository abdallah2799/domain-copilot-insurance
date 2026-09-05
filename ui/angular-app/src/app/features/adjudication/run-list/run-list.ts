import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AdjudicationService } from '../../../core/services/adjudication.service';
import { AdjudicationCase } from '../../../core/models/adjudication.model';

@Component({
  selector: 'app-run-list',
  imports: [RouterLink, DatePipe],
  templateUrl: './run-list.html',
  styleUrl: './run-list.scss',
})
export class RunList implements OnInit {
  private readonly adjudicationService = inject(AdjudicationService);

  readonly runs = signal<AdjudicationCase[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.error.set(null);
    this.adjudicationService.listRuns().subscribe({
      next: (runs) => {
        this.runs.set(runs);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(`Failed to load runs: ${err.message ?? err}`);
        this.loading.set(false);
      },
    });
  }
}
