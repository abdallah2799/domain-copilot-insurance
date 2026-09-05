import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

// UX only, same as every other role check in this app -- the API's own fallback authorization
// policy (Program.cs) is what actually blocks an unauthenticated request; this guard just avoids
// showing a page that would immediately fail every call it makes.
export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/login']);
};
