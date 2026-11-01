import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { SessionState } from '../session/session-state';
import { signInForTest } from '../../testing/session-fixture';
import { ApiError } from './api-error';
import { apiErrorInterceptor } from './api-error-interceptor';
import { authInterceptor } from './auth-interceptor';

@Component({ template: '' })
class Blank {}

function setup() {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(withInterceptors([authInterceptor, apiErrorInterceptor])),
      provideHttpClientTesting(),
      provideRouter([
        { path: 'sign-in', component: Blank },
        { path: 'change-password', component: Blank },
        { path: 'projects/:id', component: Blank },
      ]),
    ],
  });
  return {
    http: TestBed.inject(HttpClient),
    backend: TestBed.inject(HttpTestingController),
    session: TestBed.inject(SessionState),
    router: TestBed.inject(Router),
  };
}

describe('apiErrorInterceptor', () => {
  it('turns a failed API call into an ApiError the screens can read', async () => {
    const { http, backend } = setup();

    const outcome = firstValueFrom(http.put('/api/projects/1', {})).catch(
      (error: unknown) => error,
    );
    backend.expectOne('/api/projects/1').flush(
      {
        title: 'Some details need your attention',
        detail: 'Check the highlighted fields.',
        code: 'validation_failed',
        errors: { name: ['Enter the project name.'] },
      },
      { status: 422, statusText: 'Unprocessable Entity' },
    );

    const error = await outcome;
    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).errors['name']).toEqual(['Enter the project name.']);
  });

  it('hides the text of a server failure', async () => {
    const { http, backend } = setup();

    const outcome = firstValueFrom(http.get('/api/projects')).catch((error: unknown) => error);
    backend
      .expectOne('/api/projects')
      .flush('System.Exception: boom at line 9', { status: 500, statusText: 'Server Error' });

    const error = (await outcome) as ApiError;
    expect(error.isServerFailure).toBe(true);
    expect(error.message).not.toContain('boom');
  });

  it('leaves requests to other addresses alone', async () => {
    const { http, backend } = setup();

    const outcome = firstValueFrom(http.get('https://example.test/data')).catch(
      (error: unknown) => error,
    );
    backend
      .expectOne('https://example.test/data')
      .flush('nope', { status: 404, statusText: 'Not Found' });

    expect(await outcome).not.toBeInstanceOf(ApiError);
  });
});

describe('authInterceptor', () => {
  it('signs the person out and goes to sign-in, keeping the page, when the session has ended', async () => {
    const { http, backend, session, router } = setup();
    await signInForTest(session, backend);
    await router.navigateByUrl('/projects/42?tab=costs');
    expect(session.isSignedIn()).toBe(true);

    const outcome = firstValueFrom(http.get('/api/projects')).catch((error: unknown) => error);
    backend.expectOne('/api/projects').flush(null, { status: 401, statusText: 'Unauthorized' });
    await outcome;
    await new Promise((resolve) => setTimeout(resolve));

    expect(session.isSignedIn()).toBe(false);
    expect(router.url).toContain('/sign-in');
    expect(router.url).toContain('returnUrl=%2Fprojects%2F42%3Ftab%3Dcosts');
    expect(router.url).toContain('reason=expired');
  });

  it('does not redirect when nobody was signed in (a wrong password is a 401 too)', async () => {
    const { http, backend, router } = setup();
    await router.navigateByUrl('/sign-in');

    const outcome = firstValueFrom(http.post('/api/identity/sign-in', {})).catch(
      (error: unknown) => error,
    );
    backend.expectOne('/api/identity/sign-in').flush(
      {
        title: "Couldn't sign you in",
        detail: 'The username or password is incorrect.',
        code: 'sign_in_failed',
      },
      { status: 401, statusText: 'Unauthorized' },
    );

    const error = (await outcome) as ApiError;
    expect(error.detail).toBe('The username or password is incorrect.');
    expect(router.url).toBe('/sign-in');
  });

  it('sends a person who must choose a new password to the change-password page', async () => {
    const { http, backend, session, router } = setup();
    await signInForTest(session, backend);
    await router.navigateByUrl('/projects/7');

    const outcome = firstValueFrom(http.get('/api/projects')).catch((error: unknown) => error);
    backend.expectOne('/api/projects').flush(
      {
        title: 'Choose a new password first',
        detail: 'An administrator set a temporary password for you.',
        code: 'password_change_required',
      },
      { status: 403, statusText: 'Forbidden' },
    );
    await outcome;
    await new Promise((resolve) => setTimeout(resolve));

    expect(session.mustChangePassword()).toBe(true);
    expect(router.url).toBe('/change-password');
  });

  it('passes other failures through without touching the session', async () => {
    const { http, backend, session, router } = setup();
    await signInForTest(session, backend);
    await router.navigateByUrl('/projects/7');

    const outcome = firstValueFrom(http.get('/api/projects')).catch((error: unknown) => error);
    backend
      .expectOne('/api/projects')
      .flush(
        { title: 'Not allowed', detail: "You don't have access to do that.", code: 'forbidden' },
        { status: 403, statusText: 'Forbidden' },
      );
    await outcome;

    expect(session.isSignedIn()).toBe(true);
    expect(router.url).toBe('/projects/7');
  });
});
