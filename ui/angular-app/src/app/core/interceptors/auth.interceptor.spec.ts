import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { provideRouter, Router } from '@angular/router';

import { authInterceptor } from './auth.interceptor';
import { AuthService } from '../services/auth.service';

describe('authInterceptor', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), provideRouter([])],
    });
  });

  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
    localStorage.clear();
  });

  it('attaches a bearer token when a session exists', () => {
    localStorage.setItem(
      'domain-copilot.session',
      JSON.stringify({ token: 'jwt-token', username: 'adjuster', role: 'Adjuster' }),
    );
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), provideRouter([])],
    });

    TestBed.inject(HttpClient).get('/api/adjudication/runs').subscribe();

    const req = TestBed.inject(HttpTestingController).expectOne('/api/adjudication/runs');
    expect(req.request.headers.get('Authorization')).toBe('Bearer jwt-token');
    req.flush([]);
  });

  it('sends no Authorization header when logged out', () => {
    TestBed.inject(HttpClient).get('/api/adjudication/runs').subscribe();

    const req = TestBed.inject(HttpTestingController).expectOne('/api/adjudication/runs');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush([]);
  });

  // A token expires after 8 hours while the stored session does not, so without this the app keeps
  // believing it is signed in and renders a raw "Http failure response ... 401" on every page.
  it('ends the session and returns to login when the API rejects the token', async () => {
    localStorage.setItem(
      'domain-copilot.session',
      JSON.stringify({ token: 'expired-token', username: 'adjuster', role: 'Adjuster' }),
    );
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting(), provideRouter([])],
    });
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate');

    TestBed.inject(HttpClient).get('/api/documents').subscribe({ error: () => {} });
    TestBed.inject(HttpTestingController)
      .expectOne('/api/documents')
      .flush('nope', { status: 401, statusText: 'Unauthorized' });

    expect(TestBed.inject(AuthService).token).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { reason: 'session-expired' } });
  });

  // A 401 from the login endpoint means wrong credentials, and the form already says so --
  // redirecting would wipe the message the user actually needs.
  it('leaves a failed login alone', () => {
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate');

    TestBed.inject(HttpClient).post('/api/auth/login', {}).subscribe({ error: () => {} });
    TestBed.inject(HttpTestingController)
      .expectOne('/api/auth/login')
      .flush('bad', { status: 401, statusText: 'Unauthorized' });

    expect(navigate).not.toHaveBeenCalled();
  });
});
