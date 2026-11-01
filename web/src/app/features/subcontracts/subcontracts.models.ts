import { VatTreatment } from '../costing/costing.models';

// The shapes of the subcontracts API (docs/api/subcontracts.md). Money is whole minor units (centavos); rates are basis points
// (1,000 is 10%); dates are business dates ("2026-11-02"). Every figure is worked out by the server; the client only shows it.

export type SubcontractStatus = 'Draft' | 'Active' | 'Completed' | 'Cancelled';
export type ClaimStatus = 'Draft' | 'Certified' | 'Voided';
export type PaymentKind = 'Claim' | 'RetentionRelease';
export type PaymentMethod = 'BankTransfer' | 'Cheque' | 'Cash' | 'Other';
export type PaymentStatus = 'Recorded' | 'Voided';

export interface SubcontractListItem {
  readonly id: string;
  readonly number: string;
  readonly title: string;
  readonly subcontractorId: string;
  readonly subcontractorName: string;
  readonly projectId: string;
  readonly projectName: string;
  readonly subProjectName: string | null;
  readonly status: SubcontractStatus;
  readonly vatTreatment: VatTreatment;
  readonly revisedSumMinor: number;
  readonly certifiedWorkMinor: number;
  readonly percentCertifiedBps: number | null;
  /** Retention still held. */
  readonly retentionHeldMinor: number;
  /** Every recorded payment, claims and retention. */
  readonly paidMinor: number;
  /** Net payable on certified claims not yet paid. */
  readonly outstandingMinor: number;
  readonly version: number;
}

export interface SubcontractClaim {
  readonly id: string;
  /** Given when the claim is certified; null for a draft. */
  readonly claimNumber: number | null;
  readonly status: ClaimStatus;
  readonly claimDate: string;
  readonly periodEnd: string;
  readonly cumulativeWorkMinor: number;
  readonly previousCumulativeMinor: number;
  readonly thisClaimBaseMinor: number;
  readonly netMinor: number;
  readonly vatMinor: number;
  readonly grossMinor: number;
  readonly retentionMinor: number;
  readonly withholdingMinor: number;
  readonly netPayableMinor: number;
  readonly paidMinor: number;
  readonly outstandingMinor: number;
  /** A draft worked out on certified work that has changed since: save it again before certifying. */
  readonly isStale: boolean;
  readonly notes: string | null;
  readonly costEntryId: string | null;
  readonly createdByName: string | null;
  readonly createdAtUtc: string;
  readonly certifiedByName: string | null;
  readonly certifiedAtUtc: string | null;
  readonly voidedByName: string | null;
  readonly voidedAtUtc: string | null;
  readonly voidReason: string | null;
}

export interface SubcontractPayment {
  readonly id: string;
  readonly kind: PaymentKind;
  readonly claimId: string | null;
  readonly claimNumber: number | null;
  readonly amountMinor: number;
  readonly paidOn: string;
  readonly method: PaymentMethod;
  readonly reference: string | null;
  readonly note: string | null;
  readonly status: PaymentStatus;
  readonly recordedByName: string | null;
  readonly recordedAtUtc: string;
  readonly voidedByName: string | null;
  readonly voidedAtUtc: string | null;
  readonly voidReason: string | null;
}

export interface SubcontractVariation {
  readonly id: string;
  readonly number: number;
  readonly description: string;
  readonly amountMinor: number;
  readonly variationDate: string;
  readonly createdByName: string | null;
  readonly createdAtUtc: string;
}

export interface Subcontract {
  readonly id: string;
  readonly number: string;
  readonly title: string;
  readonly notes: string | null;
  readonly subcontractorId: string;
  readonly subcontractorName: string;
  readonly projectId: string;
  readonly projectName: string;
  readonly projectArchived: boolean;
  readonly subProjectId: string | null;
  readonly subProjectName: string | null;
  readonly status: SubcontractStatus;
  readonly vatTreatment: VatTreatment;
  readonly vatRateBps: number;
  readonly retentionBps: number;
  readonly withholdingBps: number;
  /** The standard work-breakdown code the certified work is counted under, or null when it is not placed in the breakdown. */
  readonly wbsCode?: string | null;
  /** The name of that work item, for display. */
  readonly workItem?: string | null;
  readonly contractSumMinor: number;
  readonly variationsMinor: number;
  readonly revisedSumMinor: number;
  readonly revisedNetMinor: number;
  readonly revisedVatMinor: number;
  readonly revisedGrossMinor: number;
  readonly certifiedWorkMinor: number;
  readonly remainingWorkMinor: number;
  readonly percentCertifiedBps: number | null;
  readonly certifiedNetMinor: number;
  readonly certifiedGrossMinor: number;
  readonly retentionHeldMinor: number;
  readonly retentionReleasedMinor: number;
  readonly retentionBalanceMinor: number;
  readonly withholdingMinor: number;
  readonly netPayableMinor: number;
  readonly paidOnClaimsMinor: number;
  readonly outstandingMinor: number;
  readonly createdByName: string | null;
  readonly createdAtUtc: string;
  readonly awardedByName: string | null;
  readonly awardedAtUtc: string | null;
  readonly completedByName: string | null;
  readonly completedAtUtc: string | null;
  readonly cancelledByName: string | null;
  readonly cancelledAtUtc: string | null;
  readonly cancelReason: string | null;
  readonly claims: readonly SubcontractClaim[];
  readonly payments: readonly SubcontractPayment[];
  readonly variations: readonly SubcontractVariation[];
  readonly version: number;
}

/** What a claim would be, worked out by the server before saving. */
export interface ClaimPreview {
  readonly cumulativeWorkMinor: number;
  readonly previousCumulativeMinor: number;
  readonly thisClaimBaseMinor: number;
  readonly netMinor: number;
  readonly vatMinor: number;
  readonly grossMinor: number;
  readonly retentionMinor: number;
  readonly withholdingMinor: number;
  readonly netPayableMinor: number;
}

export interface SubcontractTerms {
  readonly subcontractorId: string;
  readonly subProjectId: string | null;
  readonly title: string;
  readonly notes: string | null;
  readonly contractSumMinor: number | null;
  readonly vatTreatment: VatTreatment;
  readonly retentionBps: number | null;
  readonly withholdingBps: number | null;
  readonly wbsCode: string | null;
}

export interface ClaimInput {
  readonly claimDate: string;
  readonly periodEnd: string;
  readonly cumulativeWorkMinor: number | null;
  readonly notes: string | null;
}

export interface PaymentInput {
  readonly amountMinor: number | null;
  readonly paidOn: string;
  readonly method: PaymentMethod;
  readonly reference: string | null;
  readonly note: string | null;
}

export interface SubcontractListParams {
  readonly projectId: string;
  readonly status: '' | SubcontractStatus;
  readonly search: string;
  readonly page: number;
  readonly pageSize: number;
}

/** A firm the picker offers (`GET /api/suppliers/lookup?kind=Subcontractor`). */
export interface FirmChoice {
  readonly id: string;
  readonly name: string;
}

/** A project the picker offers (`GET /api/projects/lookup`). */
export interface ProjectChoice {
  readonly id: string;
  readonly name: string;
  readonly status: string;
  readonly subProjects: readonly {
    readonly id: string;
    readonly name: string;
    readonly status: string;
  }[];
}
