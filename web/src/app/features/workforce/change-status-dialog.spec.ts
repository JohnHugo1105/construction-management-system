import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, settle, text, texts, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { ChangeStatusDialog } from './change-status-dialog';
import {
  HR_PERMISSIONS,
  ruleViolation,
  validationError,
  workerWithoutPay,
} from './workforce.fixtures';
import { WorkforceApi } from './workforce-api';
import { WorkerDetail } from './workforce.models';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

async function open(worker: WorkerDetail, changeStatus: Mock<Call> = stub(() => of(worker))) {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: WorkforceApi, useValue: { changeStatus } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions: HR_PERMISSIONS,
  });
  const fixture = TestBed.createComponent(ChangeStatusDialog);
  fixture.componentRef.setInput('worker', worker);
  await settle(fixture);
  return { fixture, root: fixture.nativeElement as HTMLElement, changeStatus };
}

/** Picks a choice by the words the person reads. */
const choose = (root: HTMLElement, label: string) => {
  const row = Array.from(root.querySelectorAll<HTMLLabelElement>('label.choice')).find(
    (item) => item.querySelector('.choice__label')?.textContent?.trim() === label,
  );
  row!.querySelector<HTMLInputElement>('input')!.click();
};
const options = (root: HTMLElement) => texts(root, 'label.choice .choice__label');

describe('ChangeStatusDialog', () => {
  it('offers only the states that make sense from where the worker is, none of them chosen', async () => {
    const { root } = await open(workerWithoutPay({ status: 'Suspended' }));

    expect(options(root)).toEqual(['Active', 'On leave', 'Resigned', 'Retired', 'Terminated']);
    expect(
      Array.from(root.querySelectorAll<HTMLInputElement>('input[type="radio"]')).some(
        (input) => input.checked,
      ),
    ).toBe(false);
    expect(text(root, '.dialog__body p')).toBe('Juan Dela Cruz is now Suspended.');
  });

  it('shows a reason box only once a state is chosen, and requires it for Suspended and Terminated', async () => {
    const { root, fixture, changeStatus } = await open(workerWithoutPay());
    expect(root.querySelector('textarea')).toBeNull();

    choose(root, 'Suspended');
    await settle(fixture);
    expect(texts(root, '.field__label')).toContain('Reason');
    buttonNamed(root, 'Change status').click();
    await settle(fixture);

    expect(changeStatus).not.toHaveBeenCalled();
    expect(text(root, 'app-form-field:has(textarea) .field__error')).toBe(
      'Say why the worker is suspended.',
    );

    choose(root, 'Terminated');
    await settle(fixture);
    expect(text(root, 'app-form-field:has(textarea) .field__error')).toBe(
      'Say why the worker is terminated.',
    );
  });

  it('does not require a reason for the other states, and says so', async () => {
    const { root, fixture, changeStatus } = await open(workerWithoutPay({ version: 6 }));

    choose(root, 'On leave');
    await settle(fixture);
    expect(texts(root, '.field__label')).toContain('Reason (optional)');
    type(root, '[formControlName="effectiveDate"]', '2026-10-30');
    buttonNamed(root, 'Change status').click();
    await settle(fixture);

    expect(changeStatus).toHaveBeenCalledWith('w1', {
      status: 'OnLeave',
      effectiveDate: '2026-10-30',
      reason: null,
      version: 6,
    });
  });

  it('puts the API’s date and reason messages under their fields, and keeps the dialog open', async () => {
    const changeStatus = stub(() =>
      throwError(() =>
        validationError({
          effectiveDate: ['Choose a date between the hire date and today.'],
          reason: ['Say why the worker is terminated.'],
        }),
      ),
    );
    const { root, fixture } = await open(workerWithoutPay(), changeStatus);

    choose(root, 'Terminated');
    await settle(fixture);
    type(root, '[formControlName="reason"]', 'Repeated safety breaches');
    buttonNamed(root, 'Change status').click();
    await settle(fixture);

    expect(texts(root, '.field__error')).toEqual([
      'Say why the worker is terminated.',
      'Choose a date between the hire date and today.',
    ]);
    expect(root.querySelector('dialog[open]')).not.toBeNull();
  });

  it('shows a business rule the API enforces in its own words', async () => {
    const { root, fixture } = await open(
      workerWithoutPay(),
      stub(() => throwError(() => ruleViolation('Juan Dela Cruz is already suspended.'))),
    );

    choose(root, 'Suspended');
    await settle(fixture);
    type(root, '[formControlName="reason"]', 'Unsafe work');
    buttonNamed(root, 'Change status').click();
    await settle(fixture);

    expect(text(root, 'app-api-error-alert')).toContain('Juan Dela Cruz is already suspended.');
  });
});
