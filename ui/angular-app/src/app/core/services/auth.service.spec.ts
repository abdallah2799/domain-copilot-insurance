import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { AuthService } from './auth.service';
import { LoginResult } from '../models/auth.model';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('starts logged out when nothing is stored', () => {
    expect(service.isAuthenticated()).toBe(false);
    expect(service.token).toBeNull();
    expect(service.isAdjuster()).toBe(false);
  });

  it('stores the session and reports the role after a successful login', () => {
    const result: LoginResult = { token: 'jwt-token', username: 'adjuster', role: 'Adjuster' };

    service.login({ username: 'adjuster', password: 'secret' }).subscribe();
    httpMock.expectOne('http://localhost:5080/api/auth/login').flush(result);

    expect(service.isAuthenticated()).toBe(true);
    expect(service.token).toBe('jwt-token');
    expect(service.isAdjuster()).toBe(true);
    expect(JSON.parse(localStorage.getItem('domain-copilot.session')!)).toEqual(result);
  });

  it('reports Analyst logins as not-Adjuster', () => {
    const result: LoginResult = { token: 'jwt-token', username: 'analyst', role: 'Analyst' };

    service.login({ username: 'analyst', password: 'secret' }).subscribe();
    httpMock.expectOne('http://localhost:5080/api/auth/login').flush(result);

    expect(service.isAdjuster()).toBe(false);
  });

  it('clears the session on logout', () => {
    const result: LoginResult = { token: 'jwt-token', username: 'adjuster', role: 'Adjuster' };
    service.login({ username: 'adjuster', password: 'secret' }).subscribe();
    httpMock.expectOne('http://localhost:5080/api/auth/login').flush(result);

    service.logout();

    expect(service.isAuthenticated()).toBe(false);
    expect(service.token).toBeNull();
    expect(localStorage.getItem('domain-copilot.session')).toBeNull();
  });

  it('restores a previously stored session on construction', () => {
    localStorage.setItem(
      'domain-copilot.session',
      JSON.stringify({ token: 'stored-token', username: 'analyst', role: 'Analyst' }),
    );

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const restored = TestBed.inject(AuthService);

    expect(restored.isAuthenticated()).toBe(true);
    expect(restored.token).toBe('stored-token');
  });
});
