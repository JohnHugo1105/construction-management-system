import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, flushMicrotasks, settle, text, texts, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import {
  HR_PERMISSIONS,
  MANAGER_PERMISSIONS,
  validationError,
  workerWithPay,
} from './workforce.fixtures';
import { WorkerFormPage } from './worker-form-page';
import { WorkforceApi } from './workforce-api';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

async function open(
  permissions: readonly string[],
  hire: Mock<Call> = stub(() => of(workerWithPay({ id: 'new-1' }))),
) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'workers/new', component: WorkerFormPage },
        { path: 'workers/:id', component: Blank },
        { path: 'workers', component: Blank },
      ]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: WorkforceApi, useValue: { hire } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions,
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/workers/new', WorkerFormPage);
  await settle(harness.fixture);
  return {
    harness,
    root: harness.fixture.nativeElement as HTMLElement,
    hire,
    router: TestBed.inject(Router),
  };
}

const field = (root: HTMLElement, name: string) =>
  root.querySelector<HTMLInputElement>(`[formControlName="${name}"]`)!;
const errorFor = (control: HTMLElement) =>
  text(control.closest('app-form-field, app-field-group')!, '.field__error');

function fillIn(root: HTMLElement) {
  type(root, '[formControlName="employeeNumber"]', ' W-100 ');
  type(root, '[formControlName="firstName"]', ' Lito ');
  type(root, '[formControlName="lastName"]', 'Navarro');
  type(root, '[formControlName="designation"]', 'Mason');
  type(root, '[formControlName="dateHired"]', '2026-11-01');
  type(root, 'app-money-input input', '850.50');
  root.querySelector<HTMLInputElement>('input[type="radio"][value="Daily"]')!.click();
}

describe('WorkerFormPage', () => {
  it('asks for the pay rate and unit, and starts with neither unit chosen', async () => {
    const { root } = await open(HR_PERMISSIONS);

    expect(texts(root, '.field__label, .field__legend')).toEqual([
      'Employee number',
      'First name',
      'Last name',
      'Middle name (optional)',
      'Hire date',
      'Designation',
      'Pay rate',
      'Paid',
    ]);
    const units = Array.from(root.querySelectorAll<HTMLInputElement>('input[type="radio"]'));
    expect(units.map((unit) => unit.value)).toEqual(['Daily', 'Hourly']);
    expect(units.some((unit) => unit.checked)).toBe(false);
  });

  it('sends trimmed names, the rate in centavos, the unit and one request id, then opens the worker', async () => {
    const { root, hire, harness, router } = await open(HR_PERMISSIONS);

    fillIn(root);
    buttonNamed(root, 'Hire worker').click();
    await settle(harness.fixture);

    expect(hire).toHaveBeenCalledTimes(1);
    const sent = hire.mock.calls[0]![0] as Record<string, unknown>;
    expect(sent).toMatchObject({
      employeeNumber: 'W-100',
      firstName: 'Lito',
      middleName: null,
      lastName: 'Navarro',
      dateHired: '2026-11-01',
      designation: 'Mason',
      rateMinor: 85050,
      rateUnit: 'Daily',
    });
    expect(String(sent['clientRequestId']).length).toBeGreaterThanOrEqual(30);
    await flushMicrotasks(6);
    expect(router.url).toBe('/workers/new-1');
  });

  it('checks the form before sending anything and says what to do in plain words', async () => {
    const { root, hire, harness } = await open(HR_PERMISSIONS);

    buttonNamed(root, 'Hire worker').click();
    await settle(harness.fixture);

    expect(hire).not.toHaveBeenCalled();
    expect(texts(root, '.field__error')).toEqual([
      'Enter the employee number.',
      'Enter the first name.',
      'Enter the last name.',
      'Enter the designation, for example Mason.',
      'Enter the pay rate.',
      'Choose whether the rate is per day or per hour.',
    ]);
    expect(text(root, '.alert__title')).toBe('Some details need your attention');
  });

  it('puts each server message under the field it is about, with the API’s rate field on the pay rate', async () => {
    const hire = stub(() =>
      throwError(() =>
        validationError({
          employeeNumber: [
            'Another worker already has the employee number W-100. Use a different number.',
          ],
          dateHired: ["The hire date can't be in the future."],
          designation: ['Enter the designation (for example Mason).'],
          rateMinor: ['Enter a pay rate greater than zero.'],
          rateUnit: ['Choose whether the rate is per day or per hour.'],
        }),
      ),
    );
    const { root, harness } = await open(HR_PERMISSIONS, hire);

    fillIn(root);
    buttonNamed(root, 'Hire worker').click();
    await settle(harness.fixture);

    expect(errorFor(field(root, 'employeeNumber'))).toBe(
      'Another worker already has the employee number W-100. Use a different number.',
    );
    expect(errorFor(field(root, 'dateHired'))).toBe("The hire date can't be in the future.");
    expect(errorFor(field(root, 'designation'))).toBe('Enter the designation (for example Mason).');
    expect(errorFor(root.querySelector<HTMLInputElement>('app-money-input input')!)).toBe(
      'Enter a pay rate greater than zero.',
    );
    expect(errorFor(root.querySelector<HTMLInputElement>('input[type="radio"]')!)).toBe(
      'Choose whether the rate is per day or per hour.',
    );
    expect(field(root, 'employeeNumber').getAttribute('aria-invalid')).toBe('true');
    expect(document.activeElement).toBe(field(root, 'employeeNumber'));
    // Nothing was cleared.
    expect(field(root, 'firstName').value).toBe(' Lito ');
  });

  it('sends the same request id again after a refusal, so a retry cannot hire the person twice', async () => {
    const hire = stub(() => throwError(() => validationError({ employeeNumber: ['Taken.'] })));
    const { root, harness } = await open(HR_PERMISSIONS, hire);

    fillIn(root);
    buttonNamed(root, 'Hire worker').click();
    await settle(harness.fixture);
    type(root, '[formControlName="employeeNumber"]', 'W-101');
    buttonNamed(root, 'Hire worker').click();
    await settle(harness.fixture);

    expect(hire).toHaveBeenCalledTimes(2);
    const first = (hire.mock.calls[0]![0] as { clientRequestId: string }).clientRequestId;
    const second = (hire.mock.calls[1]![0] as { clientRequestId: string }).clientRequestId;
    expect(second).toBe(first);
  });

  it('tells a person who may not see pay that a payroll user has to hire, and shows no form', async () => {
    const { root, hire } = await open([...MANAGER_PERMISSIONS, 'workforce.manage']);

    expect(text(root, '.alert__title')).toBe('A payroll user needs to hire workers');
    expect(text(root, 'app-alert')).toContain('Ask an HR officer or an accountant');
    expect(root.querySelector('form')).toBeNull();
    expect(root.querySelector('app-money-input')).toBeNull();
    expect(hire).not.toHaveBeenCalled();
  });
});
