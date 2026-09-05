import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';

// Attaches the bearer token to every request this app makes -- the API's fallback authorization
// policy (Program.cs) requires one on every endpoint except /api/auth/login, so a request made
// while logged out simply gets a 401 from the server rather than silently succeeding.
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(AuthService).token;
  if (!token) return next(req);

  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
