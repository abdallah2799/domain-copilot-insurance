import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { authInterceptor } from './auth.interceptor';
import { AuthService } from '../services/auth.service';

describe('authInterceptor', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
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
      providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
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
});
