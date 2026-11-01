import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { CHANGE_PASSWORD_URL, SIGN_IN_URL } from '../session/auth-guards';
import { SessionState } from '../session/session-state';
import { ApiErrorCode, isApiError } from './api-error';

/**
 * Reacts to what the API says about the session, for every call:
 * - 401 while signed in: the session ended. Forget it and go to sign-in, keeping the page to come back to.
 * - 403 "password_change_required": go to the change-password page.
 * Sign-in failures are not 401s from a signed-in person, so they reach the sign-in form untouched.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const session = inject(SessionState);
  const router = inject(Router);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (isApiError(error)) {
        if (error.status === 401 && session.isSignedIn()) {
          session.clear();
          const returnUrl = router.url;
          if (!returnUrl.startsWith(SIGN_IN_URL)) {
            void router.navigate([SIGN_IN_URL], { queryParams: { returnUrl, reason: 'expired' } });
          }
        } else if (error.status === 403 && error.code === ApiErrorCode.passwordChangeRequired) {
          session.requirePasswordChange();
          if (!router.url.startsWith(CHANGE_PASSWORD_URL))
            void router.navigateByUrl(CHANGE_PASSWORD_URL);
        }
      }
      return throwError(() => error);
    }),
  );
};
