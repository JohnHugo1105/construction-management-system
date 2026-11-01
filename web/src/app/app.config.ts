import { provideHttpClient, withInterceptors, withXsrfConfiguration } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';
import { routes } from './app.routes';
import { apiErrorInterceptor } from './core/api/api-error-interceptor';
import { authInterceptor } from './core/api/auth-interceptor';
import { SessionState } from './core/session/session-state';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(
      routes,
      withComponentInputBinding(),
      withInMemoryScrolling({ scrollPositionRestoration: 'enabled' }),
    ),
    // Order matters: a failure travels back up this list, so the error mapper (last) runs first and the auth interceptor sees
    // an ApiError rather than a raw HTTP error.
    provideHttpClient(
      withInterceptors([authInterceptor, apiErrorInterceptor]),
      withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' }),
    ),
    // Before anything shows: get the anti-forgery cookie and find out whether a session already exists.
    provideAppInitializer(() => inject(SessionState).initialize()),
  ],
};
