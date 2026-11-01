import type { Progress } from '../progress/progress.models';
import { breakdownRows } from './bill-form-dialog';
import { bill, issuedBill, listBill, preview } from './billing.fixtures';
import {
  billActions,
  billName,
  billStatus,
  paymentMethodLabel,
  percentCompleteFrom,
} from './billing-rules';
import { termsSummary } from './project-billing-tab/project-billing-tab';

const APPROVER = { canManage: true, canApprove: true };
const PREPARER = { canManage: true, canApprove: false };
const READER = { canManage: false, canApprove: false };

describe('billStatus', () => {
  it('reads an issued bill by what is left to collect', () => {
    expect(billStatus(listBill({ isOverdue: true })).label).toBe('Overdue');
    expect(billStatus(listBill({ isOverdue: false })).label).toBe('Issued');
    expect(billStatus(listBill({ outstandingMinor: 0, isOverdue: false })).label).toBe('Paid');
    expect(billStatus(listBill({ status: 'Draft', number: null })).label).toBe('Draft');
    expect(billStatus(listBill({ status: 'Void', outstandingMinor: 0 })).label).toBe('Void');
  });

  it('names a bill by its number, or as a draft before it has one', () => {
    expect(billName(listBill())).toBe('BILL-0001');
    expect(billName(listBill({ number: null, status: 'Draft' }))).toBe('Draft bill');
    expect(billName(listBill({ number: null, status: 'Void' }))).toBe('Discarded draft');
    expect(paymentMethodLabel('BankTransfer')).toBe('Bank transfer');
  });
});

describe('billActions', () => {
  it('lets an approver issue a draft that is up to date, and a preparer change or discard it', () => {
    expect(billActions(bill(), APPROVER)).toMatchObject({
      issue: true,
      edit: true,
      discard: true,
      approverNote: false,
    });
    expect(billActions(bill(), PREPARER)).toMatchObject({
      issue: false,
      edit: true,
      discard: true,
      approverNote: true,
    });
    expect(billActions(bill({ isStale: true }), APPROVER).issue).toBe(false);
    expect(Object.values(billActions(bill(), READER)).every((allowed) => !allowed)).toBe(true);
  });

  it('offers a payment while something is owed, and voiding only the latest bill with nothing paid on it', () => {
    expect(billActions(issuedBill(), PREPARER)).toMatchObject({
      recordPayment: true,
      void: false,
      voidPayment: false,
    });
    expect(billActions(issuedBill(), APPROVER)).toMatchObject({
      recordPayment: true,
      void: false,
      voidPayment: true,
    });
    expect(billActions(issuedBill({ paidMinor: 0, payments: [] }), APPROVER).void).toBe(true);
    expect(billActions(issuedBill({ paidMinor: 0, isLatestIssued: false }), APPROVER).void).toBe(
      false,
    );
    expect(billActions(issuedBill({ outstandingMinor: 0 }), APPROVER).recordPayment).toBe(false);
  });

  it('offers nothing on an archived project', () => {
    const actions = billActions(issuedBill({ projectStatus: 'Archived' }), APPROVER);
    expect(Object.values(actions).every((allowed) => !allowed)).toBe(true);
  });
});

describe('percentCompleteFrom', () => {
  const progress = (hasProgress: boolean, percentCompleteBps: number | null) =>
    ({ hasProgress, earnedValue: { percentCompleteBps } }) as unknown as Progress;

  it('takes the percent complete from the progress answer once progress has been recorded', () => {
    expect(percentCompleteFrom(progress(true, 4_200))).toBe(4_200);
    expect(percentCompleteFrom(progress(true, 0))).toBe(0);
  });

  it('gives no hint when nothing has been recorded, when nothing is budgeted, or when there is no answer', () => {
    expect(percentCompleteFrom(progress(false, 0))).toBeNull();
    expect(percentCompleteFrom(progress(true, null))).toBeNull();
    expect(percentCompleteFrom(null)).toBeNull();
    expect(percentCompleteFrom(undefined)).toBeNull();
  });
});

describe('presentation', () => {
  it('prints the server’s breakdown, leaving out a retention or withholding of zero', () => {
    expect(breakdownRows(preview(), 'PHP').map((row) => `${row.label} ${row.amount}`)).toEqual([
      'This bill (VAT included) ₱3,360,000.00',
      'VAT 12%, included ₱360,000.00',
      'Retention 10%, held by the client -₱336,000.00',
      'Withholding tax 2% of the amount before VAT -₱60,000.00',
      'The client pays ₱2,964,000.00',
    ]);
    expect(
      breakdownRows(
        preview({ withholdingBps: 0, withholdingMinor: 0, netReceivableMinor: 302_400_000 }),
        'PHP',
      ).map((row) => row.label),
    ).toEqual([
      'This bill (VAT included)',
      'VAT 12%, included',
      'Retention 10%, held by the client',
      'The client pays',
    ]);
  });

  it('says the terms in one line', () => {
    expect(
      termsSummary({
        retentionBps: 1_000,
        creditableWithholdingBps: 200,
        paymentTermsDays: 30,
        isDefault: false,
        version: 1,
      }),
    ).toBe('10% retention, 2% withholding, payment due in 30 days');
    expect(
      termsSummary({
        retentionBps: 0,
        creditableWithholdingBps: 0,
        paymentTermsDays: 0,
        isDefault: true,
        version: 0,
      }),
    ).toBe('no retention, no withholding, payment due on billing');
  });
});
