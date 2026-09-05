import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { TokenUsageReport } from '../models/observability.model';

const API_BASE_URL = 'http://localhost:5080';

@Injectable({ providedIn: 'root' })
export class ObservabilityService {
  private readonly http = inject(HttpClient);

  getTokenUsage(recentLimit = 100): Observable<TokenUsageReport> {
    return this.http.get<TokenUsageReport>(`${API_BASE_URL}/api/observability/token-usage`, {
      params: { recentLimit },
    });
  }
}
