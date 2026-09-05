import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';

import { Login } from './login';

describe('Login', () => {
  let component: Login;
  let fixture: ComponentFixture<Login>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(Login);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    await fixture.whenStable();
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('navigates to /runs after a successful login', () => {
    const router = TestBed.inject(Router);
    const navigateSpy = vi.spyOn(router, 'navigateByUrl');

    component.username.set('adjuster');
    component.password.set('secret');
    component.submit();

    httpMock
      .expectOne('http://localhost:5080/api/auth/login')
      .flush({ token: 'jwt-token', username: 'adjuster', role: 'Adjuster' });

    expect(navigateSpy).toHaveBeenCalledWith('/runs');
  });

  it('shows an error message on a failed login', () => {
    component.username.set('adjuster');
    component.password.set('wrong-password');
    component.submit();

    httpMock
      .expectOne('http://localhost:5080/api/auth/login')
      .flush({ message: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    expect(component.error()).toBe('Invalid username or password.');
  });
});
