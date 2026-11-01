import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { apiErrorInterceptor } from '../../core/api/api-error-interceptor';
import { sessionFixture } from '../../testing/session-fixture';
import { buttonNamed, flushMicrotasks, settle, text, texts, type } from '../../testing/dom';
import { SetupPage } from './setup-page';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

async function open() {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'setup', component: SetupPage },
        { path: 'projects', component: Blank },
        { path: 'sign-in', component: Blank },
      ]),
      provideHttpClient(withInterceptors([apiErrorInterceptor])),
      provideHttpClientTesting(),
    ],
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/setup', SetupPage);
  return {
    harness,
    root: harness.fixture.nativeElement as HTMLElement,
    backend: TestBed.inject(HttpTestingController),
    router: TestBed.inject(Router),
  };
}

/** The hints as "done" / "not yet" words, the way a screen reader reads them. */
const hints = (root: HTMLElement) => texts(root, '.hints__item');

function fillValid(root: HTMLElement) {
  type(root, 'input[formControlName="companyName"]', 'Acme Builders');
  type(root, 'input[formControlName="displayName"]', 'Ana Reyes');
  type(root, 'input[formControlName="username"]', 'ana');
  type(root, 'input[formControlName="password"]', 'correct-horse-9');
}

describe('SetupPage password hints', () => {
  it('lists the rules, all unmet, before anything is typed', async () => {
    const { harness, root } = await open();
    await settle(harness.fixture);

    expect(hints(root)).toEqual([
      'At least 10 characters: not yet',
      'At least one letter: not yet',
      'At least one number: not yet',
    ]);
  });

  it('ticks the rules off live as the password is typed', async () => {
    const { harness, root } = await open();

    type(root, 'input[formControlName="password"]', 'abcdefghij');
    await settle(harness.fixture);
    expect(hints(root)).toEqual([
      'At least 10 characters: done',
      'At least one letter: done',
      'At least one number: not yet',
    ]);

    type(root, 'input[formControlName="password"]', 'abcdefghi1');
    await settle(harness.fixture);
    expect(root.querySelectorAll('.hints__item--met')).toHaveLength(3);
  });

  it('adds the username rule once there is a username, and drops it when the field is emptied', async () => {
    const { harness, root } = await open();

    type(root, 'input[formControlName="username"]', 'ana');
    type(root, 'input[formControlName="password"]', 'ana-password-1');
    await settle(harness.fixture);
    expect(hints(root)).toContain("Doesn't include your username: not yet");

    type(root, 'input[formControlName="username"]', '');
    await settle(harness.fixture);
    expect(hints(root).some((hint) => hint.includes('username'))).toBe(false);
  });
});

describe('SetupPage', () => {
  it('will not send a weak password, and says what to do', async () => {
    const { harness, root, backend } = await open();
    fillValid(root);
    type(root, 'input[formControlName="password"]', 'weak');

    buttonNamed(root, 'Create company').click();
    await settle(harness.fixture);

    expect(texts(root, '.field__error')).toEqual(['Make your password meet every rule below.']);
    backend.expectNone('/api/identity/bootstrap');
  });

  it('creates the company and goes to the projects', async () => {
    const { harness, root, backend, router } = await open();
    fillValid(root);

    buttonNamed(root, 'Create company').click();
    await settle(harness.fixture);
    const request = backend.expectOne('/api/identity/bootstrap');
    expect(request.request.body).toEqual({
      companyName: 'Acme Builders',
      displayName: 'Ana Reyes',
      username: 'ana',
      password: 'correct-horse-9',
    });
    request.flush(sessionFixture(), { status: 201, statusText: 'Created' });
    await flushMicrotasks(6);
    await harness.fixture.whenStable();

    expect(router.url).toBe('/projects');
  });

  it('shows the API company-name message under the company name field', async () => {
    const { harness, root, backend } = await open();
    fillValid(root);

    buttonNamed(root, 'Create company').click();
    await settle(harness.fixture);
    // The API reports the company name under the key "name".
    backend.expectOne('/api/identity/bootstrap').flush(
      {
        title: 'Some details need your attention',
        detail: 'Check the highlighted fields and try again.',
        code: 'validation_failed',
        errors: { name: ['Company name can be at most 120 characters.'] },
      },
      { status: 422, statusText: 'Unprocessable Entity' },
    );
    await flushMicrotasks(6);
    await settle(harness.fixture);

    const field = root.querySelector('input[formControlName="companyName"]')!;
    expect(field.getAttribute('aria-invalid')).toBe('true');
    expect(text(root, '.field__error')).toBe('Company name can be at most 120 characters.');
    expect(document.activeElement).toBe(field);
  });

  it('points to sign-in when the system turns out to be set up already', async () => {
    const { harness, root, backend } = await open();
    fillValid(root);

    buttonNamed(root, 'Create company').click();
    await settle(harness.fixture);
    backend.expectOne('/api/identity/bootstrap').flush(
      {
        title: "That can't be done right now",
        detail: 'This system is already set up. Sign in with your account instead.',
        code: 'rule_violation',
      },
      { status: 409, statusText: 'Conflict' },
    );
    await flushMicrotasks(6);
    await settle(harness.fixture);

    expect(text(root, '.alert__text')).toContain('This system is already set up.');
    expect(buttonNamed(root, 'Go to sign in').getAttribute('href')).toBe('/sign-in');
  });
});
