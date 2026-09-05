import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { DocumentService } from '../../core/services/document.service';
import { Document } from '../../core/models/document.model';

@Component({
  selector: 'app-ingest',
  imports: [DatePipe],
  templateUrl: './ingest.html',
  styleUrl: './ingest.scss',
})
export class Ingest implements OnInit {
  private readonly documentService = inject(DocumentService);

  readonly documents = signal<Document[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly ingesting = signal(false);
  readonly lastIngestSummary = signal<string | null>(null);

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.error.set(null);
    this.documentService.listDocuments().subscribe({
      next: (documents) => {
        this.documents.set(documents);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(`Failed to load documents: ${err.message ?? err}`);
        this.loading.set(false);
      },
    });
  }

  ingest(): void {
    this.ingesting.set(true);
    this.error.set(null);
    this.lastIngestSummary.set(null);

    this.documentService.ingestKnowledgeCorpus().subscribe({
      next: (results) => {
        const completed = results.filter((r) => r.status === 'Completed').length;
        const failed = results.filter((r) => r.status === 'Failed').length;
        this.lastIngestSummary.set(
          `Ingested ${results.length} document(s): ${completed} completed, ${failed} failed.`,
        );
        this.ingesting.set(false);
        this.refresh();
      },
      error: (err) => {
        this.ingesting.set(false);
        this.error.set(`Ingestion failed: ${err.error ?? err.message ?? err}`);
      },
    });
  }
}
