import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, flushMicrotasks, settle, text, texts, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { HR_PERMISSIONS, conflict, validationError, workerWithoutPay } from './workforce.fixtures';
import { WorkerProfilePage } from './worker-profile-page';
import { WorkforceApi } from './workforce-api';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

const worker = workerWithoutPay({ contactNumber: '0917 000 1234', version: 7 });

async function open(updateProfile: Mock<Call> = stub(() => of(worker))) {
  const get = stub(() => of(worker));
  TestBed.configureTestingModule({
    providers: [
      provideRouter(
        [
          { path: 'workers/:id/edit', component: WorkerProfilePage },
          { path: 'workers/:id', component: Blank },
        ],
        withComponentInputBinding(),
      ),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: WorkforceApi, useValue: { get, updateProfile } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions: HR_PERMISSIONS,
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/workers/w1/edit', WorkerProfilePage);
  await settle(harness.fixture);
  return {
    harness,
    root: harness.fixture.nativeElement as HTMLElement,
    get,
    updateProfile,
    router: TestBed.inject(Router),
  };
}

const field = (root: HTMLElement, name: string) =>
  root.querySelector<HTMLInputElement>(`[formControlName="${name}"]`)!;

describe('WorkerProfilePage', () => {
  it('shows the details as they are, and has no pay anywhere on it', async () => {
    const { root } = await open();

    expect(field(root, 'firstName').value).toBe('Juan');
    expect(field(root, 'contactNumber').value).toBe('0917 000 1234');
    expect(root.textContent).not.toMatch(/₱|pay rate|per day/i);
  });

  it('shows the worker’s rest day and sends the one chosen', async () => {
    const { root, harness, updateProfile } = await open();
    const restDay = root.querySelector<HTMLSelectElement>('[formControlName="restDay"]')!;
    expect(restDay.value).toBe('None');

    restDay.value = 'Sunday';
    restDay.dispatchEvent(new Event('change', { bubbles: true }));
    buttonNamed(root, 'Save profile').click();
    await settle(harness.fixture);

    expect(updateProfile.mock.calls[0]![1]).toMatchObject({ restDay: 'Sunday', version: 7 });
  });

  it('sends the version it opened, with blank optional boxes as nothing', async () => {
    const { root, harness, updateProfile, router } = await open();

    type(root, '[formControlName="lastName"]', ' Dela Cruz Jr ');
    type(root, '[formControlName="contactNumber"]', '   ');
    buttonNamed(root, 'Save profile').click();
    await settle(harness.fixture);

    expect(updateProfile).toHaveBeenCalledTimes(1);
    expect(updateProfile.mock.calls[0]![0]).toBe('w1');
    expect(updateProfile.mock.calls[0]![1]).toMatchObject({
      firstName: 'Juan',
      lastName: 'Dela Cruz Jr',
      contactNumber: null,
      dateOfBirth: null,
      version: 7,
    });
    await flushMicrotasks(6);
    expect(router.url).toBe('/workers/w1');
  });

  it('puts the server’s messages under the right fields', async () => {
    const updateProfile = stub(() =>
      throwError(() =>
        validationError({
          dateOfBirth: ['Enter a valid date of birth.'],
          lastName: ['Enter the last name.'],
        }),
      ),
    );
    const { root, harness } = await open(updateProfile);

    type(root, '[formControlName="dateOfBirth"]', '2999-01-01');
    buttonNamed(root, 'Save profile').click();
    await settle(harness.fixture);

    expect(texts(root, '.field__error')).toEqual([
      'Enter the last name.',
      'Enter a valid date of birth.',
    ]);
    expect(text(root, 'app-api-error-alert')).toContain('Check the highlighted fields');
  });

  it('asks the person to reload when someone else changed the worker', async () => {
    const { root, harness, get } = await open(stub(() => throwError(() => conflict())));

    buttonNamed(root, 'Save profile').click();
    await settle(harness.fixture);
    expect(text(root, 'app-api-error-alert')).toContain('Someone else changed this record');

    buttonNamed(root, 'Reload worker').click();
    await settle(harness.fixture);
    expect(get).toHaveBeenCalledTimes(2);
  });
});
