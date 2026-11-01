import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { apiErrorInterceptor } from '../../core/api/api-error-interceptor';
import { authInterceptor } from '../../core/api/auth-interceptor';
import { sessionFixture } from '../../testing/session-fixture';
import { buttonNamed, flushMicrotasks, settle, text, texts, type } from '../../testing/dom';
import { SignInPage } from './sign-in-page';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

async function open(url = '/sign-in', needsSetup = false) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'sign-in', component: SignInPage },
        { path: 'setup', component: Blank },
        { path: 'projects', component: Blank },
        { path: 'projects/:id', component: Blank },
      ]),
      provideHttpClient(withInterceptors([authInterceptor, apiErrorInterceptor])),
      provideHttpClientTesting(),
    ],
  });
  const backend = TestBed.inject(HttpTestingController);
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(url, SignInPage);

  // On arrival the page asks for a fresh anti-forgery cookie, then whether first-time setup is still open.
  backend.expectOne('/api/identity/csrf').flush(null, { status: 204, statusText: 'No Content' });
  await flushMicrotasks();
  backend.expectOne('/api/identity/setup-status').flush({ needsSetup });
  await settle(harness.fixture);

  const root = harness.fixture.nativeElement as HTMLElement;
  return { harness, root, backend, router: TestBed.inject(Router) };
}

const usernameField = (root: HTMLElement) =>
  root.querySelector<HTMLInputElement>('input[formControlName="username"]')!;
const passwordField = (root: HTMLElement) =>
  root.querySelector<HTMLInputElement>('input[formControlName="password"]')!;

describe('SignInPage', () => {
  it('asks for both fields before sending anything, and puts the keyboard on the first missing one', async () => {
    const { harness, root, backend } = await open();

    buttonNamed(root, 'Sign in').click();
    await settle(harness.fixture);

    expect(texts(root, '.field__error')).toEqual(['Enter your username.', 'Enter your password.']);
    expect(usernameField(root).getAttribute('aria-invalid')).toBe('true');
    expect(document.activeElement).toBe(usernameField(root));
    backend.expectNone('/api/identity/sign-in');
  });

  it('signs in with the trimmed username and goes back to the page that was asked for', async () => {
    const { harness, root, backend, router } = await open('/sign-in?returnUrl=%2Fprojects%2F42');

    type(root, 'input[formControlName="username"]', '  ana  ');
    type(root, 'input[formControlName="password"]', 'a-long-password-1');
    buttonNamed(root, 'Sign in').click();
    await settle(harness.fixture);

    const request = backend.expectOne('/api/identity/sign-in');
    expect(request.request.body).toEqual({ username: 'ana', password: 'a-long-password-1' });
    request.flush(sessionFixture());
    await flushMicrotasks(6);
    await harness.fixture.whenStable();

    expect(router.url).toBe('/projects/42');
  });

  it('refuses a return address that points outside the app and goes home instead', async () => {
    const { harness, root, backend, router } = await open(
      '/sign-in?returnUrl=https%3A%2F%2Fevil.example',
    );

    type(root, 'input[formControlName="username"]', 'ana');
    type(root, 'input[formControlName="password"]', 'a-long-password-1');
    buttonNamed(root, 'Sign in').click();
    await settle(harness.fixture);
    backend.expectOne('/api/identity/sign-in').flush(sessionFixture());
    await flushMicrotasks(6);
    await harness.fixture.whenStable();

    expect(router.url).toBe('/');
  });

  it('says the username or password is wrong without blaming either field', async () => {
    const { harness, root, backend } = await open();

    type(root, 'input[formControlName="username"]', 'ana');
    type(root, 'input[formControlName="password"]', 'wrong-password-1');
    buttonNamed(root, 'Sign in').click();
    await settle(harness.fixture);
    backend.expectOne('/api/identity/sign-in').flush(
      {
        title: "Couldn't sign you in",
        detail: 'The username or password is incorrect.',
        code: 'sign_in_failed',
      },
      { status: 401, statusText: 'Unauthorized' },
    );
    await flushMicrotasks(6);
    await settle(harness.fixture);

    expect(text(root, '.alert__title')).toBe("Couldn't sign you in");
    expect(text(root, '.alert__text')).toContain('The username or password is incorrect.');
    expect(root.querySelectorAll('[aria-invalid="true"]')).toHaveLength(0);
    expect(root.querySelectorAll('.field__error')).toHaveLength(0);
    // The username stays, the password is cleared and ready to retype.
    expect(usernameField(root).value).toBe('ana');
    expect(passwordField(root).value).toBe('');
    expect(document.activeElement).toBe(passwordField(root));
  });

  it('shows the lockout message in the API words', async () => {
    const { harness, root, backend } = await open();

    type(root, 'input[formControlName="username"]', 'ana');
    type(root, 'input[formControlName="password"]', 'wrong-password-1');
    buttonNamed(root, 'Sign in').click();
    await settle(harness.fixture);
    backend.expectOne('/api/identity/sign-in').flush(
      {
        title: "Couldn't sign you in",
        detail:
          'This account is locked for now after too many wrong passwords. Try again in 14 minutes.',
        code: 'sign_in_failed',
      },
      { status: 401, statusText: 'Unauthorized' },
    );
    await flushMicrotasks(6);
    await settle(harness.fixture);

    expect(text(root, '.alert__text')).toContain('Try again in 14 minutes.');
  });

  it('keeps server text out of a failure and offers the reference to quote', async () => {
    const { harness, root, backend } = await open();

    type(root, 'input[formControlName="username"]', 'ana');
    type(root, 'input[formControlName="password"]', 'a-long-password-1');
    buttonNamed(root, 'Sign in').click();
    await settle(harness.fixture);
    backend
      .expectOne('/api/identity/sign-in')
      .flush('System.Data.SqlClient.SqlException: timeout', {
        status: 500,
        statusText: 'Server Error',
        headers: { 'X-Correlation-Id': 'ref-123' },
      });
    await flushMicrotasks(6);
    await settle(harness.fixture);

    expect(text(root, 'app-api-error-alert')).toContain(
      "We couldn't finish that. Try again in a moment.",
    );
    expect(text(root, 'app-api-error-alert')).toContain('Reference: ref-123');
    expect(text(root, 'app-api-error-alert')).not.toContain('SqlException');
  });

  it('offers first-time setup only while the system has no users', async () => {
    const needed = await open('/sign-in', true);
    expect(buttonNamed(needed.root, 'Set up your company').getAttribute('href')).toBe('/setup');
    TestBed.resetTestingModule();

    const notNeeded = await open('/sign-in', false);
    expect(notNeeded.root.textContent).not.toContain('Set up your company');
  });

  it('explains why the person is back at sign-in after their session ended', async () => {
    const { root } = await open('/sign-in?reason=expired&returnUrl=%2Fprojects');

    expect(text(root, 'app-alert')).toContain('You were signed out because your session ended.');
  });
});
