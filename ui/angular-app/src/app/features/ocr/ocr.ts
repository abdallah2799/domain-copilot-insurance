import { Component, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { OcrService } from '../../core/services/ocr.service';
import { ScannedDocument } from '../../core/models/ocr.model';

@Component({
  selector: 'app-ocr',
  imports: [FormsModule, DecimalPipe],
  templateUrl: './ocr.html',
  styleUrl: './ocr.scss',
})
export class Ocr {
  private readonly ocrService = inject(OcrService);

  readonly claimNumber = signal('');
  readonly selectedFile = signal<File | null>(null);
  readonly uploading = signal(false);
  readonly error = signal<string | null>(null);
  readonly result = signal<ScannedDocument | null>(null);

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile.set(input.files?.[0] ?? null);
  }

  submit(): void {
    const file = this.selectedFile();
    const claimNumber = this.claimNumber().trim();
    if (!file || !claimNumber) return;

    this.uploading.set(true);
    this.error.set(null);
    this.result.set(null);

    this.ocrService.uploadDocument(claimNumber, file).subscribe({
      next: (document) => {
        this.result.set(document);
        this.uploading.set(false);
      },
      error: (err) => {
        this.error.set(`Upload failed: ${err.error ?? err.message ?? err}`);
        this.uploading.set(false);
      },
    });
  }
}
