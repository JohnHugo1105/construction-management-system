import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ApiError } from '../../../core/api/api-error';
import { SessionState } from '../../../core/session/session-state';
import { buttonNamed, flushMicrotasks, settle, text, texts, type } from '../../../testing/dom';
import { signInForTest } from '../../../testing/session-fixture';
import { UserFormPage } from './user-form-page';
import { UsersApi } from './users-api';
import { UserAccount } from './users.models';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

const maria: UserAccount = {
  id: 'u2',
  username: 'maria',
  displayName: 'Maria Santos',
  email: 'maria@example.test',
  roles: ['ProjectManager', 'Accountant'],
  isActive: true,
  mustChangePassword: false,
  lastSignInUtc: null,
  lockedUntilUtc: null,
  version: 6,
};

async function open(url: string, api: Partial<Record<'create' | 'update' | 'get', Mock<Call>>>) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter(
        [
          { path: 'admin/users/new', component: UserFormPage },
          { path: 'admin/users/:id/edit', component: UserFormPage },
          { path: 'admin/users', component: Blank },
        ],
        withComponentInputBinding(),
      ),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: UsersApi, useValue: api },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController));
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(url, UserFormPage);
  await settle(harness.fixture);
  return {
    harness,
    root: harness.fixture.nativeElement as HTMLElement,
    router: TestBed.inject(Router),
  };
}

const roleBox = (root: HTMLElement, label: string) =>
  Array.from(root.querySelectorAll<HTMLLabelElement>('label.choice'))
    .find((choice) => choice.querySelector('.choice__label')?.textContent?.trim() === label)!
    .querySelector<HTMLInputElement>('input')!;

function fillNewUser(root: HTMLElement) {
  type(root, '[formControlName="displayName"]', '  Maria Santos ');
  type(root, '[formControlName="username"]', 'maria');
  type(root, '[formControlName="email"]', ' ');
  type(root, '[formControlName="password"]', 'Temporary-2026-a');
}

describe('UserFormPage (new user)', () => {
  it('offers every role with a plain line about what it is for, as a labelled group', async () => {
    const { root } = await open('/admin/users/new', {});

    const group = root.querySelector('fieldset')!;
    expect(group.querySelector('legend')!.textContent).toContain('Roles');
    expect(texts(group, '.choice__label')).toEqual([
      'Administrator',
      'Project Manager',
      'Cost Engineer',
      'Accountant',
      'Warehouse Keeper',
      'HR Officer',
      'Viewer',
    ]);
    expect(group.querySelectorAll('.choice__hint')).toHaveLength(7);
    expect(group.querySelectorAll('input[type="checkbox"]:checked')).toHaveLength(0);
  });

  it('asks for a role, and says so under the group', async () => {
    const create = stub(() => of(maria));
    const { harness, root } = await open('/admin/users/new', { create });
    fillNewUser(root);

    buttonNamed(root, 'Create user').click();
    await settle(harness.fixture);

    expect(text(root.querySelector('fieldset')!, '.field__error')).toBe(
      'Choose at least one role.',
    );
    expect(create).not.toHaveBeenCalled();
  });

  it('creates the user with the chosen roles and a temporary password, then returns to the list', async () => {
    const create = stub(() => of(maria));
    const { harness, root, router } = await open('/admin/users/new', { create });
    fillNewUser(root);

    roleBox(root, 'Project Manager').click();
    roleBox(root, 'Accountant').click();
    roleBox(root, 'Viewer').click();
    roleBox(root, 'Viewer').click();
    buttonNamed(root, 'Create user').click();
    await settle(harness.fixture);

    expect(create).toHaveBeenCalledWith({
      username: 'maria',
      displayName: 'Maria Santos',
      email: null,
      password: 'Temporary-2026-a',
      roles: ['ProjectManager', 'Accountant'],
    });
    await flushMicrotasks(6);
    expect(router.url).toBe('/admin/users');
  });

  it('shows what the temporary password needs, ticking as it is typed', async () => {
    const { harness, root } = await open('/admin/users/new', {});

    type(root, '[formControlName="password"]', 'short');
    await settle(harness.fixture);

    expect(texts(root, '.hints__item')).toEqual([
      'At least 10 characters: not yet',
      'At least one letter: done',
      'At least one number: not yet',
    ]);
  });

  it('puts "that username is taken" under the username', async () => {
    const create = stub(() =>
      throwError(
        () =>
          new ApiError({
            status: 422,
            title: 'Some details need your attention',
            detail: 'Check the highlighted fields and try again.',
            errors: {
              username: ['That username is taken. Choose another.'],
              password: ['Include at least one number.'],
            },
          }),
      ),
    );
    const { harness, root } = await open('/admin/users/new', { create });
    fillNewUser(root);
    roleBox(root, 'Viewer').click();

    buttonNamed(root, 'Create user').click();
    await settle(harness.fixture);

    const errorFor = (name: string) =>
      text(
        root.querySelector(`[formControlName="${name}"]`)!.closest('app-form-field')!,
        '.field__error',
      );
    expect(errorFor('username')).toBe('That username is taken. Choose another.');
    expect(errorFor('password')).toBe('Include at least one number.');
  });
});

describe('UserFormPage (editing)', () => {
  it('shows the person with their roles ticked, and the username as fixed text', async () => {
    const get = stub(() => of(maria));
    const { root } = await open('/admin/users/u2/edit', { get });

    expect((root.querySelector('[formControlName="displayName"]') as HTMLInputElement).value).toBe(
      'Maria Santos',
    );
    expect(root.querySelector('[formControlName="username"]')).toBeNull();
    expect(text(root, '.field p:nth-of-type(2)')).toBe('maria');
    expect(root.querySelector('[formControlName="password"]')).toBeNull();
    expect(roleBox(root, 'Project Manager').checked).toBe(true);
    expect(roleBox(root, 'Accountant').checked).toBe(true);
    expect(roleBox(root, 'Viewer').checked).toBe(false);
  });

  it('saves with the version it opened', async () => {
    const get = stub(() => of(maria));
    const update = stub(() => of({ ...maria, version: 7 }));
    const { harness, root } = await open('/admin/users/u2/edit', { get, update });

    roleBox(root, 'Accountant').click();
    buttonNamed(root, 'Save user').click();
    await settle(harness.fixture);

    expect(update).toHaveBeenCalledWith('u2', {
      displayName: 'Maria Santos',
      email: 'maria@example.test',
      roles: ['ProjectManager'],
      version: 6,
    });
  });

  it('shows the API refusal about the last administrator in its own words, above the form', async () => {
    const admin: UserAccount = { ...maria, roles: ['Administrator'] };
    const update = stub(() =>
      throwError(
        () =>
          new ApiError({
            status: 409,
            title: "That can't be done right now",
            detail:
              'There must always be at least one active administrator. Make someone else an administrator first.',
            code: 'rule_violation',
          }),
      ),
    );
    const { harness, root } = await open('/admin/users/u2/edit', {
      get: stub(() => of(admin)),
      update,
    });

    roleBox(root, 'Administrator').click();
    roleBox(root, 'Viewer').click();
    buttonNamed(root, 'Save user').click();
    await settle(harness.fixture);

    expect(text(root, 'app-api-error-alert')).toContain(
      'There must always be at least one active administrator.',
    );
    expect(root.querySelectorAll('.field__error')).toHaveLength(0);
  });
});
