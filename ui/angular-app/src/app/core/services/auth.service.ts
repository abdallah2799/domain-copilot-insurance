import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { LoginRequest, LoginResult } from '../models/auth.model';

const API_BASE_URL = 'http://localhost:5080';
const STORAGE_KEY = 'domain-copilot.session';

// The whole LoginResult (token + username + role) is stored together, not just the raw JWT --
// role-gating the UI (isAdjuster) reads it directly rather than decoding the token client-side,
// which would need a JWT-decoding library for something the login response already hands us.
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  readonly session = signal<LoginResult | null>(this.readStoredSession());

  login(request: LoginRequest): Observable<LoginResult> {
    return this.http.post<LoginResult>(`${API_BASE_URL}/api/auth/login`, request).pipe(
      tap((result) => {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(result));
        this.session.set(result);
      }),
    );
  }

  logout(): void {
    localStorage.removeItem(STORAGE_KEY);
    this.session.set(null);
  }

  get token(): string | null {
    return this.session()?.token ?? null;
  }

  isAuthenticated(): boolean {
    return this.session() !== null;
  }

  isAdjuster(): boolean {
    return this.session()?.role === 'Adjuster';
  }

  private readStoredSession(): LoginResult | null {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;

    try {
      return JSON.parse(raw) as LoginResult;
    } catch {
      // Corrupted/foreign value in this key -- treat as logged out rather than throwing on load.
      return null;
    }
  }
}
