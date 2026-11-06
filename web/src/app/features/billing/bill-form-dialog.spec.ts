import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ApiError } from '../../core/api/api-error';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { settle, text, texts, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { ruleViolation, validationError } from '../workforce/workforce.fixtures';
import { BillFormContext, BillFormDialog } from './bill-form-dialog';
import { BillingApi } from './billing-api';
import { WITH_ADVANCE, advanceBill, bill, preview } from './billing.fixtures';
import { Bill } from './billing.models';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

const CONTEXT: BillFormContext = {
  projectId: 'p1',
  contractPriceMinor: 1_120_000_000,
  billedToDateMinor: 224_000_000,
  retentionHeldMinor: 22_400_000,
  advanceBps: 0,
  advanceToBillMinor: null,
};

@Component({
  imports: [BillFormDialog],
  template: `<app-bill-form-dialog [context]="context" (saved)="saved.set($event)" />`,
})
class Host {
  readonly context = CONTEXT;
  readonly saved = signal<Bill | null>(null);
}

/** A project whose terms take a 15% down payment that hasn't been billed: the server says it comes to ₱1,680,000.00. */
@Component({
  imports: [BillFormDialog],
  template: `<app-bill-form-dialog
    [context]="context"
    kind="Advance"
    (saved)="saved.set($event)"
  />`,
})
class AdvanceHost {
  readonly context: BillFormContext = {
    ...CONTEXT,
    billedToDateMinor: 0,
    retentionHeldMinor: 0,
    advanceBps: 1_500,
    advanceToBillMinor: 168_000_000,
  };
  readonly saved = signal<Bill | null>(null);
}

/** A down payment draft at 20% after the terms changed to 15%: its own figures are out of date. */
@Component({
  imports: [BillFormDialog],
  template: `<app-bill-form-dialog
    [context]="context"
    [bill]="draft"
    (saved)="saved.set($event)"
  />`,
})
class StaleAdvanceHost {
  readonly context: BillFormContext = { ...CONTEXT, billedToDateMinor: 0, advanceBps: 1_500 };
  readonly draft = advanceBill({
    status: 'Draft',
    isStale: true,
    advanceBps: 2_000,
    grossMinor: 224_000_000,
    netReceivableMinor: 224_000_000,
  });
  readonly saved = signal<Bill | null>(null);
}

async function open(
  api: Record<string, Mock<Call>>,
  permissions: readonly string[],
  host: typeof Host | typeof AdvanceHost | typeof StaleAdvanceHost = Host,
) {
  vi.useFakeTimers({ shouldAdvanceTime: true });
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: BillingApi, useValue: api },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions,
  });
  const fixture = TestBed.createComponent<Host | AdvanceHost | StaleAdvanceHost>(host);
  await settle(fixture);
  return { fixture, dialog: () => document.querySelector('app-bill-form-dialog')! };
}

async function typeWork(
  fixture: Awaited<ReturnType<typeof open>>['fixture'],
  value: string,
): Promise<void> {
  type(document, 'app-bill-form-dialog app-money-input input', value);
  await vi.advanceTimersByTimeAsync(400);
  await settle(fixture);
}

const MANAGER = [Permission.projectsRead, Permission.billingRead, Permission.billingManage];

afterEach(() => vi.useRealTimers());

describe('BillFormDialog', () => {
  it('asks the server what the bill comes to and shows its breakdown before saving', async () => {
    const previewCall = stub(() => of(preview()));
    const { fixture, dialog } = await open(
      { preview: previewCall, progress: stub(() => of({})) },
      MANAGER,
    );

    expect(text(dialog(), '.field__hint')).toContain(
      'Already billed: ₱2,240,000.00 of the ₱11,200,000.00 contract.',
    );
    await typeWork(fixture, '5,600,000');

    expect(previewCall).toHaveBeenLastCalledWith(
      'p1',
      560_000_000,
      expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/),
    );
    expect(texts(dialog(), '.breakdown__row')).toEqual([
      'This bill (VAT included)₱3,360,000.00',
      'VAT 12%, included₱360,000.00',
      'Retention 10%, held by the client-₱336,000.00',
      'Withholding tax 2% of the amount before VAT-₱60,000.00',
      'The client pays₱2,964,000.00',
    ]);
  });

  it('puts the server’s refusal of the amount under the field', async () => {
    const previewCall = stub(() =>
      throwError(() =>
        validationError({
          cumulativeWorkMinor: ["Work completed to date can't be more than the contract price."],
        }),
      ),
    );
    const { fixture, dialog } = await open({ preview: previewCall }, MANAGER);

    await typeWork(fixture, '99,000,000');

    expect(text(dialog(), '.field__error')).toBe(
      "Work completed to date can't be more than the contract price.",
    );
    expect(dialog().querySelector('.breakdown__row')).toBeNull();
  });

  it('shows the VAT withheld and the down payment recovered in the server’s breakdown', async () => {
    const { fixture, dialog } = await open(
      { preview: stub(() => of(preview({ ...WITH_ADVANCE }))) },
      MANAGER,
    );

    await typeWork(fixture, '5,600,000');

    expect(texts(dialog(), '.breakdown__row')).toEqual([
      'This bill (VAT included)₱3,360,000.00',
      'VAT 12%, included₱360,000.00',
      'Retention 10%, held by the client-₱336,000.00',
      'Withholding tax 2% of the amount before VAT-₱60,000.00',
      'VAT withheld, 5% of the amount before VAT-₱150,000.00',
      'Down payment recovered-₱504,000.00',
      'The client pays₱2,310,000.00',
    ]);
  });

  it('shows a refusal of the figures, such as deductions more than the bill, under the field', async () => {
    const message =
      'The deductions are more than this bill. Check the down payment, retention and withholding rates.';
    const { fixture, dialog } = await open(
      { preview: stub(() => throwError(() => ruleViolation(message))) },
      MANAGER,
    );

    await typeWork(fixture, '2,250,000');

    expect(text(dialog(), '.field__error')).toBe(message);
    expect(dialog().querySelector('.breakdown__row')).toBeNull();
  });

  it('bills the down payment the server worked out, with no amount to type', async () => {
    const create = stub(() => of(advanceBill()));
    const previewCall = stub(() => of(preview()));
    const { fixture, dialog } = await open(
      { preview: previewCall, create },
      MANAGER,
      AdvanceHost,
    );

    expect(text(dialog(), 'h2')).toBe('Bill the down payment');
    expect(dialog().querySelector('app-money-input')).toBeNull();
    expect(texts(dialog(), '.breakdown__row')).toEqual([
      'Down payment, 15% of the contract price₱1,680,000.00',
      'The client pays₱1,680,000.00',
    ]);

    (dialog().querySelector('button[type="submit"]') as HTMLButtonElement).click();
    await settle(fixture);

    expect(previewCall).not.toHaveBeenCalled();
    expect(create).toHaveBeenCalledWith(
      'p1',
      expect.objectContaining({
        kind: 'Advance',
        cumulativeWorkMinor: null,
        amountMinor: null,
        description: 'Down payment',
      }),
    );
    expect(fixture.componentInstance.saved()?.kind).toBe('Advance');
  });

  it('does not show an out-of-date down payment draft’s old figures: saving works them out again', async () => {
    const { dialog } = await open({}, MANAGER, StaleAdvanceHost);

    expect(text(dialog(), 'h2')).toBe('Change draft bill');
    expect(dialog().querySelector('.breakdown__row')).toBeNull();
    expect(text(dialog(), '.breakdown')).toContain(
      'Worked out from the contract price and the billing terms when you save.',
    );
  });

  it('suggests the percent complete when progress answers, and says nothing when it refuses', async () => {
    const answered = await open(
      {
        preview: stub(() => of(preview())),
        progress: stub(() => of({ hasProgress: true, earnedValue: { percentCompleteBps: 5_250 } })),
      },
      [...MANAGER, Permission.progressRead],
    );
    expect(text(answered.dialog(), '.suggestion')).toBe(
      'Suggested from progress: the work is 52.5% complete.',
    );
    TestBed.resetTestingModule();
    vi.useRealTimers();

    const refused = await open(
      {
        preview: stub(() => of(preview())),
        progress: stub(() =>
          throwError(() => new ApiError({ status: 404, title: 'Not found', detail: '' })),
        ),
      },
      [...MANAGER, Permission.progressRead],
    );
    expect(refused.dialog().querySelector('.suggestion')).toBeNull();
    TestBed.resetTestingModule();
    vi.useRealTimers();

    const progress = stub(() =>
      of({ hasProgress: true, earnedValue: { percentCompleteBps: 1_000 } }),
    );
    const notAllowed = await open({ preview: stub(() => of(preview())), progress }, MANAGER);
    expect(progress).not.toHaveBeenCalled();
    expect(notAllowed.dialog().querySelector('.suggestion')).toBeNull();
  });

  it('saves a draft with a request id, so a repeated click can not make two', async () => {
    const create = stub(() => of(bill()));
    const { fixture, dialog } = await open({ preview: stub(() => of(preview())), create }, MANAGER);

    await typeWork(fixture, '5,600,000');
    (dialog().querySelector('button[type="submit"]') as HTMLButtonElement).click();
    await settle(fixture);

    expect(create).toHaveBeenCalledWith(
      'p1',
      expect.objectContaining({
        kind: 'Progress',
        cumulativeWorkMinor: 560_000_000,
        amountMinor: null,
        description: 'Progress billing',
        clientRequestId: expect.any(String),
      }),
    );
    expect(fixture.componentInstance.saved()?.id).toBe('b2');
  });
});
