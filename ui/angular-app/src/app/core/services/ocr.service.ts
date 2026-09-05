import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ScannedDocument } from '../models/ocr.model';

const API_BASE_URL = 'http://localhost:5080';

@Injectable({ providedIn: 'root' })
export class OcrService {
  private readonly http = inject(HttpClient);

  // A multipart upload, not JSON -- the API accepts the raw scanned PDF plus the claim number as
  // a form field (OcrController.UploadDocument).
  uploadDocument(claimNumber: string, file: File): Observable<ScannedDocument> {
    const formData = new FormData();
    formData.append('claimNumber', claimNumber);
    formData.append('file', file, file.name);
    return this.http.post<ScannedDocument>(`${API_BASE_URL}/api/ocr/documents`, formData);
  }

  listForClaim(claimNumber: string): Observable<ScannedDocument[]> {
    return this.http.get<ScannedDocument[]>(`${API_BASE_URL}/api/ocr/documents`, {
      params: { claimNumber },
    });
  }
}
