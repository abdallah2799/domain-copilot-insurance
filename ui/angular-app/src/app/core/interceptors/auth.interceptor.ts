import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

// Attaches the bearer token to every request made through HttpClient -- the API's fallback
// authorization policy (Program.cs) requires one on every endpoint except /api/auth/login.
//
// It does NOT cover raw fetch calls, which the two SSE streams use because EventSource cannot send
// a header or a POST body. Those attach the token themselves; this is called out because assuming
// otherwise is exactly how the Ask stream shipped unauthenticated while the /ask endpoint next to
// it worked.
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const token = authService.token;

  const authorized = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(authorized).pipe(
    catchError((error: unknown) => {
      // A token expires after JWT_EXPIRY_MINUTES (8 hours by default) while the session stored in
      // localStorage does not, so the app would go on believing it was signed in and render a raw
      // "Http failure response ... 401" on every page. Ending the dead session and returning to the
      // login screen is both the honest reaction and the only one a user can act on.
      //
      // The login request itself is excluded: a 401 there means wrong credentials, and the login
      // form already reports that. Redirecting would only wipe the message the user needs to see.
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !req.url.endsWith('/api/auth/login')
      ) {
        authService.logout();
        router.navigate(['/login'], { queryParams: { reason: 'session-expired' } });
      }

      return throwError(() => error);
    }),
  );
};
