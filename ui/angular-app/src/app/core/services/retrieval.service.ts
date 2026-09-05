import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AskRequest, AskResult } from '../models/retrieval.model';

const API_BASE_URL = 'http://localhost:5080';

@Injectable({ providedIn: 'root' })
export class RetrievalService {
  private readonly http = inject(HttpClient);

  // A refused question returns immediately (no LLM call, per AskService); a grounded one makes one
  // real completion call, so this is a fraction of a second to tens of seconds depending on the
  // provider, not the minutes a full adjudication run takes.
  ask(request: AskRequest): Observable<AskResult> {
    return this.http.post<AskResult>(`${API_BASE_URL}/api/retrieval/ask`, request);
  }
}
