import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { toApiError } from './api-error';

/** Only our own API is mapped; a request to anywhere else keeps its raw error. */
export function isApiRequest(url: string): boolean {
  return url.startsWith('/api/') || url.startsWith('/health');
}

/**
 * Turns every failed API call into an ApiError, so screens never see raw HTTP errors or server text. It runs closest to the
 * network; the auth interceptor above it reads the mapped error.
 */
export const apiErrorInterceptor: HttpInterceptorFn = (request, next) =>
  next(request).pipe(
    catchError((failure: unknown) => {
      if (!isApiRequest(request.url) || !(failure instanceof HttpErrorResponse))
        return throwError(() => failure);
      return throwError(() => toApiError(failure));
    }),
  );
