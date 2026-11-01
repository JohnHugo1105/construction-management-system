import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { PageTitleState } from '../../core/layout/page-title-state';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, settle, text, texts, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { ruleViolation } from '../workforce/workforce.fixtures';
import { BillPage } from './bill-page';
import { BillingApi } from './billing-api';
import { WITH_ADVANCE, advanceBill, bill, issuedBill } from './billing.fixtures';
import { Bill } from './billing.models';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

const READER = [Permission.projectsRead, Permission.billingRead];
const PREPARER = [...READER, Permission.billingManage];
const APPROVER = [...PREPARER, Permission.billingApprove];

async function open(
  current: Bill,
  permissions: readonly string[],
  api: Record<string, Mock<Call>> = {},
) {
  const get = stub(() => of(current));
  TestBed.configureTestingModule({
    providers: [
      provideRouter(
        [{ path: 'billing/bills/:id', component: BillPage }],
        withComponentInputBinding(),
      ),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: BillingApi, useValue: { get, ...api } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions,
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(`/billing/bills/${current.id}`, BillPage);
  await settle(harness.fixture);
  return { fixture: harness.fixture, root: harness.fixture.nativeElement as HTMLElement };
}

const actions = (root: HTMLElement) => texts(root, '.head__actions .btn');

describe('BillPage', () => {
  it('shows a down payment bill as its share of the contract price, nothing withheld', async () => {
    const { root } = await open(advanceBill(), READER);

    expect(text(root, '.head__kind')).toBe('Down payment · 31 Oct 2026');
    expect(texts(root, '.rows__row')).toEqual([
      'Down payment, 15% of the contract price₱1,680,000.00',
      'The client pays₱1,680,000.00',
    ]);
  });

  it('adds the VAT withheld and the down payment recovered to a progress bill’s breakdown', async () => {
    const { root } = await open(bill({ ...WITH_ADVANCE }), READER);

    expect(texts(root, '.rows__row').slice(-3)).toEqual([
      'VAT withheld, 5% of the amount before VAT-₱150,000.00',
      'Down payment recovered-₱504,000.00',
      'The client pays₱2,310,000.00',
    ]);
  });

  it('shows a draft’s breakdown from the server and lets an approver issue it', async () => {
    const issue = stub(() => of(issuedBill()));
    const { root, fixture } = await open(bill(), APPROVER, { issue });

    expect(TestBed.inject(PageTitleState).title()).toBe('Draft bill');
    expect(actions(root)).toEqual(['Issue bill', 'Change draft', 'Discard draft']);
    expect(texts(root, '.rows__row')).toEqual([
      'Work completed to date₱5,600,000.00',
      'Billed before this bill₱2,240,000.00',
      'This bill (VAT included)₱3,360,000.00',
      'VAT 12%, included₱360,000.00',
      'Retention 10%, held by the client-₱336,000.00',
      'Withholding tax 2% of the amount before VAT-₱60,000.00',
      'The client pays₱2,964,000.00',
    ]);

    buttonNamed(root, 'Issue bill').click();
    await settle(fixture);

    expect(issue).toHaveBeenCalledWith('b2', 1);
    expect(TestBed.inject(PageTitleState).title()).toBe('BILL-0002');
    expect(texts(root, '.totals__item')).toEqual([
      'Still to collect₱2,864,000.00',
      'Paid₱100,000.00',
      'Due30 Nov 2026',
    ]);
  });

  it('tells a preparer who issues bills, and holds back a draft another bill made out of date', async () => {
    const preparer = await open(bill(), PREPARER);
    expect(actions(preparer.root)).toEqual(['Change draft', 'Discard draft']);
    expect(text(preparer.root, 'app-alert .alert__title')).toBe('An accountant issues bills');
    TestBed.resetTestingModule();

    const stale = await open(bill({ isStale: true }), APPROVER);
    expect(actions(stale.root)).toEqual(['Change draft', 'Discard draft']);
    expect(text(stale.root, 'app-alert .alert__title')).toBe('This draft is out of date');
    expect(text(stale.root, 'app-alert')).toContain('Another bill was issued or voided');
  });

  it('holds back a down payment draft the contract price or the terms made out of date', async () => {
    const { root } = await open(advanceBill({ status: 'Draft', isStale: true }), APPROVER);

    expect(actions(root)).toEqual(['Change draft', 'Discard draft']);
    expect(text(root, 'app-alert .alert__title')).toBe('This draft is out of date');
    expect(text(root, 'app-alert')).toContain(
      'The contract price or the down payment in the billing terms changed after it was prepared.',
    );
  });

  it('offers only discarding a down payment draft once a progress bill is issued, and says why', async () => {
    const { root } = await open(
      advanceBill({ status: 'Draft', isStale: true, canOnlyBeDiscarded: true }),
      APPROVER,
    );

    expect(actions(root)).toEqual(['Discard draft']);
    expect(root.querySelector('app-alert')).toBeNull();
    expect(text(root, '.stack > p.text-muted')).toBe(
      'A progress bill has been issued, so this down payment can no longer be billed. Discard this draft.',
    );
  });

  it('shows the refusal in plain words when issuing fails', async () => {
    const issue = stub(() =>
      throwError(() =>
        ruleViolation('Another bill was issued or voided after this draft was prepared.'),
      ),
    );
    const { root, fixture } = await open(bill(), APPROVER, { issue });

    buttonNamed(root, 'Issue bill').click();
    await settle(fixture);

    expect(text(root, 'app-api-error-alert')).toContain(
      'Another bill was issued or voided after this draft was prepared.',
    );
  });

  it('offers recording a payment while something is owed, and voiding a payment to an approver', async () => {
    const recordPayment = stub(() =>
      of(issuedBill({ paidMinor: 296_400_000, outstandingMinor: 0 })),
    );
    const { root, fixture } = await open(issuedBill(), APPROVER, { recordPayment });

    expect(actions(root)).toEqual(['Record payment']);
    expect(texts(root, '.payment .payment__main')[0]).toContain('₱100,000.00');
    expect(texts(root, '.payment button')).toEqual(['Void payment']);

    buttonNamed(root, 'Record payment').click();
    await settle(fixture);
    const dialog = document.querySelector('app-payment-dialog')!;
    expect((dialog.querySelector('app-money-input input') as HTMLInputElement).value).toBe(
      '2,864,000.00',
    );
    type(dialog, 'input[formcontrolname="reference"]', 'BDO 7781');
    (dialog.querySelector('button[type="submit"]') as HTMLButtonElement).click();
    await settle(fixture);

    expect(recordPayment).toHaveBeenCalledWith(
      'b2',
      expect.objectContaining({
        amountMinor: 286_400_000,
        method: 'BankTransfer',
        reference: 'BDO 7781',
        version: 4,
      }),
    );
    expect(text(root, '.head app-status-chip')).toBe('Paid');
    expect(actions(root)).toEqual([]);
  });

  it('lets an approver void the latest bill with nothing paid on it, asking why', async () => {
    const voided = issuedBill({
      status: 'Void',
      paidMinor: 0,
      outstandingMinor: 0,
      payments: [],
      voidReason: 'Wrong period',
      version: 5,
    });
    const voidCall = stub(() => of(voided));
    const { root, fixture } = await open(
      issuedBill({ paidMinor: 0, outstandingMinor: 296_400_000, payments: [] }),
      APPROVER,
      {
        void: voidCall,
      },
    );

    expect(actions(root)).toEqual(['Record payment', 'Void bill']);
    buttonNamed(root, 'Void bill').click();
    await settle(fixture);
    const dialog = document.querySelector('app-reason-dialog')!;
    type(dialog, 'textarea', 'Wrong period');
    (dialog.querySelector('button[type="submit"]') as HTMLButtonElement).click();
    await settle(fixture);

    expect(voidCall).toHaveBeenCalledWith('b2', 'Wrong period', 4);
    expect(text(root, 'app-alert')).toContain('Reason: Wrong period.');
    expect(actions(root)).toEqual([]);
  });

  it('shows the figures only to a person who may read', async () => {
    const { root } = await open(issuedBill(), READER);
    expect(actions(root)).toEqual([]);
    expect(root.querySelectorAll('.payment button').length).toBe(0);
  });
});
