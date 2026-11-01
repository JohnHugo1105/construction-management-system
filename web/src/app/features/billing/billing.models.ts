// The billing API's shapes (src/Cms.Application/Features/Billing/BillingContracts.cs). Money is whole minor units; rates are basis
// points; dates are business dates ("2026-10-31"). Every figure here was worked out by the server.

import { PagedResult } from '../../core/api/paged-result';

export type BillKind = 'Progress' | 'RetentionRelease' | 'Advance';
export type BillStatus = 'Draft' | 'Issued' | 'Void';
export type PaymentMethod = 'BankTransfer' | 'Cheque' | 'Cash' | 'Other';

export interface BillingTerms {
  readonly retentionBps: number;
  readonly creditableWithholdingBps: number;
  readonly paymentTermsDays: number;
  /** The down payment as a share of the contract price; 0 for none. */
  readonly advanceBps: number;
  /** The share of the amount before VAT the client withholds as VAT. */
  readonly vatWithholdingBps: number;
  readonly isGovernmentClient: boolean;
  /** True while the project's terms were never saved and the defaults apply. */
  readonly isDefault: boolean;
  readonly version: number;
}

export interface BillListItem {
  readonly id: string;
  readonly number: string | null;
  readonly projectId: string;
  readonly projectName: string;
  readonly kind: BillKind;
  readonly status: BillStatus;
  readonly billingDate: string;
  readonly dueDate: string;
  readonly description: string;
  readonly grossMinor: number;
  readonly netReceivableMinor: number;
  readonly paidMinor: number;
  readonly outstandingMinor: number;
  readonly isOverdue: boolean;
  readonly version: number;
}

export interface ProjectBilling {
  readonly projectId: string;
  readonly projectName: string;
  readonly projectStatus: string;
  readonly contractPriceMinor: number | null;
  readonly billedToDateMinor: number;
  readonly percentBilledBps: number | null;
  readonly remainingToBillMinor: number | null;
  readonly retentionWithheldMinor: number;
  readonly retentionReleasedMinor: number;
  readonly retentionHeldMinor: number;
  readonly collectedMinor: number;
  readonly outstandingMinor: number;
  readonly overdueMinor: number;
  readonly overdueCount: number;
  readonly canReleaseRetention: boolean;
  /** The issued down payment, what issued progress bills recovered of it, and the rest. */
  readonly advanceBilledMinor: number;
  readonly advanceRecoveredMinor: number;
  readonly advanceToRecoverMinor: number;
  /** The project's down payment bill that is not void (draft or issued), if any. */
  readonly advanceBillId: string | null;
  /** What a down payment bill would ask for now; null while one can't be prepared. */
  readonly advanceToBillMinor: number | null;
  readonly terms: BillingTerms;
  readonly vatRateBps: number;
  readonly bills: PagedResult<BillListItem>;
}

export interface BillPayment {
  readonly id: string;
  readonly amountMinor: number;
  readonly paidOn: string;
  readonly method: PaymentMethod;
  readonly reference: string | null;
  readonly note: string | null;
  readonly recordedByName: string | null;
  readonly recordedAtUtc: string;
  readonly isVoided: boolean;
  readonly voidedByName: string | null;
  readonly voidedAtUtc: string | null;
  readonly voidReason: string | null;
}

export interface Bill {
  readonly id: string;
  readonly number: string | null;
  readonly projectId: string;
  readonly projectName: string;
  readonly projectStatus: string;
  readonly contractPriceMinor: number | null;
  readonly kind: BillKind;
  readonly status: BillStatus;
  readonly billingDate: string;
  readonly dueDate: string;
  readonly description: string;
  readonly cumulativeWorkMinor: number | null;
  readonly previousCumulativeWorkMinor: number;
  readonly grossMinor: number;
  readonly netMinor: number;
  readonly vatMinor: number;
  readonly vatRateBps: number;
  readonly retentionBps: number;
  readonly retentionMinor: number;
  readonly withholdingBps: number;
  readonly withholdingMinor: number;
  readonly vatWithholdingBps: number;
  readonly vatWithholdingMinor: number;
  /**
   * The down payment bill's own rate, or the issued down payment's rate on a progress bill. A progress bill recovers in proportion to
   * its work against the contract price, so its recovery is shown as an amount, never as this rate.
   */
  readonly advanceBps: number;
  readonly advanceRecoveryMinor: number;
  readonly netReceivableMinor: number;
  readonly paymentTermsDays: number;
  readonly paidMinor: number;
  readonly outstandingMinor: number;
  readonly isOverdue: boolean;
  /** The project's latest issued bill: the only one that can be voided. */
  readonly isLatestIssued: boolean;
  /**
   * A draft whose figures no longer follow from what is billed now (a down payment: from the contract price and the terms now); it must
   * be saved again before it is issued.
   */
  readonly isStale: boolean;
  /** A down payment draft once a progress bill is issued: it can't be issued or changed any more, only discarded. */
  readonly canOnlyBeDiscarded: boolean;
  readonly createdByName: string | null;
  readonly createdAtUtc: string;
  readonly issuedByName: string | null;
  readonly issuedAtUtc: string | null;
  readonly voidedByName: string | null;
  readonly voidedAtUtc: string | null;
  readonly voidReason: string | null;
  readonly payments: readonly BillPayment[];
  readonly version: number;
}

export interface BillPreview {
  readonly contractPriceMinor: number;
  readonly previousCumulativeWorkMinor: number;
  readonly cumulativeWorkMinor: number;
  readonly grossMinor: number;
  readonly netMinor: number;
  readonly vatMinor: number;
  readonly vatRateBps: number;
  readonly retentionBps: number;
  readonly retentionMinor: number;
  readonly withholdingBps: number;
  readonly withholdingMinor: number;
  readonly vatWithholdingBps: number;
  readonly vatWithholdingMinor: number;
  readonly advanceRecoveryMinor: number;
  readonly netReceivableMinor: number;
  readonly paymentTermsDays: number;
  readonly dueDate: string;
}

export interface ReceivableRow {
  readonly projectId: string;
  readonly projectName: string;
  readonly projectStatus: string;
  readonly billedToDateMinor: number;
  readonly collectedMinor: number;
  readonly outstandingMinor: number;
  readonly overdueMinor: number;
  readonly retentionHeldMinor: number;
}

export interface Receivables {
  readonly outstandingMinor: number;
  readonly overdueMinor: number;
  readonly retentionHeldMinor: number;
  readonly rows: PagedResult<ReceivableRow>;
}

export interface SaveTermsRequest {
  readonly retentionBps: number;
  readonly creditableWithholdingBps: number;
  readonly paymentTermsDays: number;
  readonly advanceBps: number;
  readonly vatWithholdingBps: number;
  readonly isGovernmentClient: boolean;
  readonly version: number;
}

export interface CreateBillRequest {
  readonly kind: BillKind;
  readonly billingDate: string;
  readonly description: string;
  readonly cumulativeWorkMinor: number | null;
  readonly amountMinor: number | null;
  readonly clientRequestId: string;
}

export interface UpdateBillRequest {
  readonly billingDate: string;
  readonly description: string;
  readonly cumulativeWorkMinor: number | null;
  readonly amountMinor: number | null;
  readonly version: number;
}

export interface RecordPaymentRequest {
  readonly amountMinor: number;
  readonly paidOn: string;
  readonly method: PaymentMethod;
  readonly reference: string | null;
  readonly note: string | null;
  readonly clientRequestId: string;
  readonly version: number;
}
