import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { SessionState } from '../../core/session/session-state';
import { addDays, todayIn } from '../../shared/presentation/business-time';
import { buttonNamed, flushMicrotasks, settle, text, texts, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { ruleViolation, validationError } from '../workforce/workforce.fixtures';
import { PayrollApi } from './payroll-api';
import { PayrollNewPage } from './payroll-new-page';
import { defaultPeriod } from './payroll-rules';
import { run } from './payroll.fixtures';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

const today = todayIn('Asia/Manila');

async function open(create: Mock<Call> = stub(() => of(run({ id: 'run-9' })))) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'payroll/new', component: PayrollNewPage },
        { path: 'payroll/:id', component: Blank },
        { path: 'payroll', component: Blank },
      ]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: PayrollApi, useValue: { create } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController));
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/payroll/new', PayrollNewPage);
  await settle(harness.fixture);
  return {
    harness,
    root: harness.fixture.nativeElement as HTMLElement,
    create,
    router: TestBed.inject(Router),
  };
}

const start = (root: HTMLElement) =>
  root.querySelector<HTMLInputElement>('[formControlName="periodStart"]')!;
const end = (root: HTMLElement) =>
  root.querySelector<HTMLInputElement>('[formControlName="periodEnd"]')!;
const errorFor = (control: HTMLElement) =>
  text(control.closest('app-form-field')!, '.field__error');
const create = (root: HTMLElement) => buttonNamed(root, 'Create payroll run');

describe('PayrollNewPage', () => {
  it('offers the 14 days that ended yesterday, and says what creating does', async () => {
    const { root } = await open();

    const period = defaultPeriod(today);
    expect(start(root).value).toBe(period.start);
    expect(end(root).value).toBe(period.end);
    expect(period.end).toBe(addDays(today, -1));
    expect(end(root).max).toBe(today);
    expect(text(root, '.form-card > p')).toBe(
      "Takes all attendance that hasn't been paid and locks it. Locked attendance can't be changed until the payroll run is voided.",
    );
  });

  it('creates the run for the period shown, with one request id, and opens it', async () => {
    const { root, harness, create: api, router } = await open();

    create(root).click();
    await settle(harness.fixture);

    expect(api).toHaveBeenCalledTimes(1);
    const sent = api.mock.calls[0]![0] as {
      periodStart: string;
      periodEnd: string;
      clientRequestId: string;
    };
    expect(sent.periodStart).toBe(defaultPeriod(today).start);
    expect(sent.periodEnd).toBe(defaultPeriod(today).end);
    expect(sent.clientRequestId.length).toBeGreaterThanOrEqual(30);
    await flushMicrotasks(6);
    expect(router.url).toBe('/payroll/run-9');
  });

  it('refuses a last day in the future, before the first day, or a period longer than 31 days', async () => {
    const { root, harness, create: api } = await open();

    type(root, '[formControlName="periodEnd"]', addDays(today, 1)!);
    create(root).click();
    await settle(harness.fixture);
    expect(errorFor(end(root))).toBe("The pay period can't end in the future.");

    type(root, '[formControlName="periodEnd"]', '2026-10-01');
    type(root, '[formControlName="periodStart"]', '2026-10-05');
    await settle(harness.fixture);
    expect(errorFor(end(root))).toBe("The last day can't be before the first day.");

    type(root, '[formControlName="periodStart"]', '2025-01-01');
    type(root, '[formControlName="periodEnd"]', today);
    await settle(harness.fixture);
    expect(errorFor(end(root))).toBe('A pay period is at most 31 days.');

    create(root).click();
    await settle(harness.fixture);
    expect(api).not.toHaveBeenCalled();
    expect(text(root, '.alert__title')).toBe('Some details need your attention');
  });

  it('clears a "last day" message as soon as the first day is moved so the two fit', async () => {
    const { root, harness } = await open();
    type(root, '[formControlName="periodEnd"]', '2026-10-01');
    type(root, '[formControlName="periodStart"]', '2026-10-05');
    end(root).dispatchEvent(new Event('blur'));
    await settle(harness.fixture);
    expect(errorFor(end(root))).toBe("The last day can't be before the first day.");

    type(root, '[formControlName="periodStart"]', '2026-09-20');
    await settle(harness.fixture);

    expect(root.querySelectorAll('.field__error')).toHaveLength(0);
  });

  it('says plainly when there is no unpaid attendance, and keeps the person on the form', async () => {
    const api = stub(() =>
      throwError(() => ruleViolation('There is no unpaid attendance in that period.')),
    );
    const { root, harness, router } = await open(api);

    create(root).click();
    await settle(harness.fixture);

    expect(text(root, 'app-api-error-alert')).toContain(
      'There is no unpaid attendance in that period.',
    );
    expect(router.url).toBe('/payroll/new');
    // The dates are still there to change.
    expect(start(root).value).toBe(defaultPeriod(today).start);
  });

  it('puts the server’s message under the day it is about, and keeps one request id for a retry', async () => {
    const api = stub(() =>
      throwError(() => validationError({ periodEnd: ['A pay period is at most 31 days.'] })),
    );
    const { root, harness } = await open(api);

    create(root).click();
    await settle(harness.fixture);
    expect(texts(root, '.field__error')).toEqual(['A pay period is at most 31 days.']);
    // Changing the day clears the message, and the retry carries the same request id.
    type(root, '[formControlName="periodStart"]', addDays(today, -20)!);
    create(root).click();
    await settle(harness.fixture);

    expect(api).toHaveBeenCalledTimes(2);
    const ids = api.mock.calls.map(
      (call) => (call[0] as { clientRequestId: string }).clientRequestId,
    );
    expect(ids[1]).toBe(ids[0]);
  });
});
