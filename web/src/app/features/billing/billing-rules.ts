import type { Progress } from '../progress/progress.models';
import { IconName } from '../../shared/ui/icon/icon';
import { ChipTone } from '../../shared/ui/status-chip/status-chip';
import { Bill, BillKind, BillListItem, PaymentMethod } from './billing.models';

// How billing is presented, in one place, so a list row and the bill page describe a bill the same way. Nothing here calculates
// money: every amount comes from the server.

export interface StatusPresentation {
  readonly label: string;
  readonly tone: ChipTone;
  readonly icon: IconName;
}

/** The state a person sees. An issued bill reads by what is left to collect, which the server worked out. */
export function billStatus(
  bill: Pick<BillListItem, 'status' | 'outstandingMinor' | 'isOverdue'>,
): StatusPresentation {
  switch (bill.status) {
    case 'Draft':
      return { label: 'Draft', tone: 'warning', icon: 'pencil' };
    case 'Void':
      return { label: 'Void', tone: 'neutral', icon: 'minus-circle' };
    default:
      if (bill.outstandingMinor <= 0)
        return { label: 'Paid', tone: 'success', icon: 'check-circle' };
      if (bill.isOverdue) return { label: 'Overdue', tone: 'danger', icon: 'clock' };
      return { label: 'Issued', tone: 'info', icon: 'receipt' };
  }
}

export const BILL_KIND: Record<BillKind, string> = {
  Progress: 'Progress bill',
  RetentionRelease: 'Retention release',
  Advance: 'Down payment',
};

export const PAYMENT_METHODS: readonly { readonly value: PaymentMethod; readonly label: string }[] =
  [
    { value: 'BankTransfer', label: 'Bank transfer' },
    { value: 'Cheque', label: 'Cheque' },
    { value: 'Cash', label: 'Cash' },
    { value: 'Other', label: 'Other' },
  ];

export function paymentMethodLabel(method: PaymentMethod): string {
  return PAYMENT_METHODS.find((item) => item.value === method)?.label ?? method;
}

/** "BILL-0003", or "Draft bill" before a number is given at issue. */
export function billName(bill: Pick<BillListItem, 'number' | 'status'>): string {
  return bill.number ?? (bill.status === 'Draft' ? 'Draft bill' : 'Discarded draft');
}

export interface BillPermissions {
  readonly canManage: boolean;
  readonly canApprove: boolean;
}

/** What a person may do with a bill now. An action they may not use is absent, never greyed out. */
export interface BillActions {
  readonly issue: boolean;
  readonly edit: boolean;
  readonly discard: boolean;
  readonly recordPayment: boolean;
  readonly void: boolean;
  readonly voidPayment: boolean;
  /** A preparer looking at a draft: say who issues it. */
  readonly approverNote: boolean;
}

const NOTHING: BillActions = {
  issue: false,
  edit: false,
  discard: false,
  recordPayment: false,
  void: false,
  voidPayment: false,
  approverNote: false,
};

export function billActions(bill: Bill, who: BillPermissions): BillActions {
  // An archived project is read-only.
  if (bill.projectStatus === 'Archived') return NOTHING;
  const draft = bill.status === 'Draft';
  const issued = bill.status === 'Issued';
  return {
    issue: draft && who.canApprove && !bill.isStale,
    edit: draft && who.canManage,
    discard: draft && who.canManage,
    recordPayment: issued && who.canManage && bill.outstandingMinor > 0,
    void: issued && who.canApprove && bill.isLatestIssued && bill.paidMinor === 0,
    voidPayment: issued && who.canApprove,
    approverNote: draft && who.canManage && !who.canApprove,
  };
}

/**
 * The project's percent complete from the progress answer, as basis points; null when nobody has recorded progress yet (a hint of "0% of the
 * contract" would say something nobody measured). The billing form only shows it as a hint.
 */
export function percentCompleteFrom(progress: Progress | null | undefined): number | null {
  if (!progress?.hasProgress) return null;
  return progress.earnedValue.percentCompleteBps;
}
