import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { SessionState } from '../../core/session/session-state';
import { todayIn } from '../../shared/presentation/business-time';
import { buttonNamed, settle, text, texts, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { ChangeRateDialog } from './change-rate-dialog';
import {
  HR_PERMISSIONS,
  conflict,
  rate,
  validationError,
  workerWithPay,
} from './workforce.fixtures';
import { WorkforceApi } from './workforce-api';
import { WorkerDetail } from './workforce.models';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

async function open(worker: WorkerDetail, changeRate: Mock<Call> = stub(() => of(worker))) {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: WorkforceApi, useValue: { changeRate } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions: HR_PERMISSIONS,
  });
  const fixture = TestBed.createComponent(ChangeRateDialog);
  fixture.componentRef.setInput('worker', worker);
  const changed = vi.fn();
  fixture.componentInstance.changed.subscribe(changed);
  await settle(fixture);
  return { fixture, root: fixture.nativeElement as HTMLElement, changeRate, changed };
}

/** Picks a kind of change by the words the person reads. */
const choose = (root: HTMLElement, label: string) => {
  const row = Array.from(root.querySelectorAll<HTMLLabelElement>('label.choice')).find(
    (item) => item.querySelector('.choice__label')?.textContent?.trim() === label,
  );
  row!.querySelector<HTMLInputElement>('input')!.click();
};
const designation = (root: HTMLElement) =>
  root.querySelector<HTMLInputElement>('[formControlName="designation"]')!;
const errorFor = (control: HTMLElement) =>
  text(control.closest('app-form-field, app-field-group')!, '.field__error');
const save = (root: HTMLElement) => buttonNamed(root, 'Save pay rate');

describe('ChangeRateDialog', () => {
  it('describes the rate the worker is paid now and starts with no kind of change chosen', async () => {
    const { root } = await open(workerWithPay());

    expect(text(root, '.dialog__body p')).toContain(
      'Juan Dela Cruz is now paid ₱800.00 per day as Mason.',
    );
    const kinds = Array.from(
      root.querySelectorAll<HTMLInputElement>('input[type="radio"][formControlName="category"]'),
    );
    expect(texts(root, 'label.choice:has([formControlName="category"]) .choice__label')).toEqual([
      'Promotion',
      'Demotion',
      'Rate adjustment',
    ]);
    expect(kinds.some((kind) => kind.checked)).toBe(false);
    // The unit they are paid in is already there; the new rate and the reason are theirs to give.
    expect(
      root.querySelector<HTMLInputElement>('input[type="radio"][value="Daily"]')!.checked,
    ).toBe(true);
  });

  it('asks for a new designation on a promotion and refuses the one the worker already has', async () => {
    const { root, fixture, changeRate } = await open(workerWithPay());

    choose(root, 'Promotion');
    await settle(fixture);
    expect(designation(root).disabled).toBe(false);
    expect(designation(root).value).toBe('');
    type(root, '[formControlName="designation"]', 'mason');
    type(root, 'app-money-input input', '950');
    save(root).click();
    await settle(fixture);

    expect(changeRate).not.toHaveBeenCalled();
    expect(errorFor(designation(root))).toBe(
      'A promotion or demotion changes the designation. Enter the new designation, or choose rate adjustment.',
    );
  });

  it('keeps the designation for a rate adjustment: it is filled in and cannot be typed over', async () => {
    const { root, fixture } = await open(workerWithPay());

    choose(root, 'Rate adjustment');
    await settle(fixture);

    expect(designation(root).value).toBe('Mason');
    expect(designation(root).disabled).toBe(true);
    expect(
      text(root, 'app-form-field:has([formControlName="designation"]) .field__hint'),
    ).toContain('keeps the designation');

    // Going back to a promotion gives the field back, empty, so the new designation is typed on purpose.
    choose(root, 'Demotion');
    await settle(fixture);
    expect(designation(root).disabled).toBe(false);
    expect(designation(root).value).toBe('');
  });

  it('sends the category, designation, rate in centavos, unit, start date and the worker’s version', async () => {
    const { root, fixture, changeRate, changed } = await open(workerWithPay({ version: 9 }));

    choose(root, 'Promotion');
    await settle(fixture);
    type(root, '[formControlName="designation"]', ' Foreman ');
    type(root, 'app-money-input input', '950.00');
    type(root, '[formControlName="effectiveFrom"]', '2026-11-15');
    type(root, '[formControlName="reason"]', 'Led the structure crew');
    save(root).click();
    await settle(fixture);

    expect(changeRate).toHaveBeenCalledWith('w1', {
      category: 'Promotion',
      designation: 'Foreman',
      rateMinor: 95000,
      rateUnit: 'Daily',
      effectiveFrom: '2026-11-15',
      reason: 'Led the structure crew',
      version: 9,
    });
    expect(changed).toHaveBeenCalledTimes(1);
  });

  it('starts the new rate after the latest one in the pay history, not on a day that is taken', async () => {
    const today = todayIn('Asia/Manila');
    const hiredToday = workerWithPay({ dateHired: today, rates: [rate({ effectiveFrom: today })] });
    const { root } = await open(hiredToday);

    const start = root.querySelector<HTMLInputElement>('[formControlName="effectiveFrom"]')!;
    expect(start.value > today).toBe(true);
    expect(start.min).toBe(start.value);
    expect(
      text(root, 'app-form-field:has([formControlName="effectiveFrom"]) .field__hint'),
    ).toContain('when the current rate started');
  });

  it('puts the API’s messages under the right fields, with its rate field on the new pay rate', async () => {
    const changeRate = stub(() =>
      throwError(() =>
        validationError({
          effectiveFrom: [
            'Choose a date after 1 Mar 2026, when the current rate started. Earlier pay isn’t rewritten.',
          ],
          designation: [
            'A promotion or demotion changes the designation. Enter the new designation, or choose rate adjustment.',
          ],
          rateMinor: ['That is the current rate. Enter a different amount.'],
        }),
      ),
    );
    const { root, fixture } = await open(workerWithPay(), changeRate);

    choose(root, 'Promotion');
    await settle(fixture);
    type(root, '[formControlName="designation"]', 'Foreman');
    type(root, 'app-money-input input', '800');
    save(root).click();
    await settle(fixture);

    expect(changeRate).toHaveBeenCalledTimes(1);
    expect(
      errorFor(root.querySelector<HTMLInputElement>('[formControlName="effectiveFrom"]')!),
    ).toContain('Choose a date after 1 Mar 2026');
    expect(errorFor(designation(root))).toContain(
      'A promotion or demotion changes the designation',
    );
    expect(errorFor(root.querySelector<HTMLInputElement>('app-money-input input')!)).toBe(
      'That is the current rate. Enter a different amount.',
    );
    // Nothing the person typed was cleared.
    expect(root.querySelector<HTMLInputElement>('app-money-input input')!.value).toBe('800');
  });

  it('asks the person to reload when someone else changed the worker', async () => {
    const { root, fixture } = await open(
      workerWithPay(),
      stub(() => throwError(() => conflict())),
    );

    choose(root, 'Rate adjustment');
    type(root, 'app-money-input input', '900');
    save(root).click();
    await settle(fixture);

    expect(text(root, 'app-api-error-alert')).toContain('Someone else changed this record');
    expect(texts(root, 'app-api-error-alert button')).toEqual(['Reload worker']);
  });
});
