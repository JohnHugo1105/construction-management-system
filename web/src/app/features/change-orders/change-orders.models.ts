// The shapes of the change order API (docs/api/change-orders.md). Money is whole minor units (centavos), signed where it is a change.
// The client shows what the server sends and never works out an effect, a revised budget or a price itself.

import { ItemType, VatTreatment } from '../costing/costing.models';

export type ChangeOrderStatus = 'Draft' | 'Submitted' | 'Approved' | 'Rejected' | 'Voided';

export type ChangeOrderReason =
  'ClientRequest' | 'DesignChange' | 'SiteCondition' | 'ErrorOrOmission' | 'Other';

export type ChangeOrderLineKind = 'Add' | 'Revise' | 'Omit';

/** A project's budget explained: original (frozen at lock), approved changes, revised. */
export interface BudgetBaseline {
  readonly projectId: string;
  readonly locked: boolean;
  readonly lockedAtUtc: string | null;
  readonly lockedBy: string | null;
  readonly originalBudgetNetMinor: number | null;
  readonly originalBudgetGrossMinor: number | null;
  readonly originalContractPriceMinor: number | null;
  readonly approvedCount: number;
  readonly approvedBudgetNetMinor: number;
  readonly approvedBudgetGrossMinor: number;
  readonly approvedContractPriceDeltaMinor: number;
  readonly revisedBudgetNetMinor: number;
  readonly revisedBudgetGrossMinor: number;
  readonly revisedContractPriceMinor: number | null;
  readonly activeLineCount: number;
  readonly currentBudgetNetMinor: number;
  readonly currentBudgetGrossMinor: number;
  readonly currentContractPriceMinor: number | null;
  readonly pendingCount: number;
  readonly pendingBudgetNetMinor: number;
  readonly pendingBudgetGrossMinor: number;
  readonly pendingContractPriceDeltaMinor: number;
  /** While locked: the active budget lines add up to original + approved changes. */
  readonly reconciles: boolean;
  readonly lastUnlockReason: string | null;
  readonly lastUnlockedAtUtc: string | null;
  readonly projectVersion: number;
}

export interface ChangeOrderListItem {
  readonly id: string;
  readonly projectId: string;
  /** "CO-001"; null while a draft. */
  readonly number: string | null;
  readonly title: string;
  readonly reason: ChangeOrderReason;
  readonly status: ChangeOrderStatus;
  readonly voidedFromStatus: ChangeOrderStatus | null;
  readonly lineCount: number;
  readonly budgetEffectNetMinor: number;
  readonly budgetEffectGrossMinor: number;
  readonly contractPriceDeltaMinor: number;
  readonly createdAtUtc: string;
  readonly submittedAtUtc: string | null;
  readonly decidedAtUtc: string | null;
  readonly voidedAtUtc: string | null;
  readonly version: number;
}

export interface ChangeOrderLine {
  readonly id: string;
  readonly lineNumber: number;
  readonly kind: ChangeOrderLineKind;
  readonly budgetLineId: string | null;
  readonly subProjectId: string | null;
  readonly subProjectName: string | null;
  readonly division: number | null;
  readonly mainScope: string | null;
  readonly subScope: string | null;
  readonly wbsCode: string | null;
  readonly description: string;
  readonly itemType: ItemType;
  readonly quantity: number;
  readonly unit: string;
  readonly unitCostMinor: number;
  readonly vatTreatment: VatTreatment;
  readonly vatRateBps: number;
  readonly entryDate: string;
  readonly remarks: string | null;
  readonly netMinor: number;
  readonly vatMinor: number;
  readonly grossMinor: number;
  readonly previousQuantity: number | null;
  readonly previousUnitCostMinor: number | null;
  readonly previousNetMinor: number | null;
  readonly previousGrossMinor: number | null;
  readonly effectNetMinor: number;
  readonly effectVatMinor: number;
  readonly effectGrossMinor: number;
}

export interface ChangeOrder {
  readonly id: string;
  readonly projectId: string;
  readonly number: string | null;
  readonly title: string;
  readonly description: string | null;
  readonly reason: ChangeOrderReason;
  readonly status: ChangeOrderStatus;
  readonly voidedFromStatus: ChangeOrderStatus | null;
  readonly contractPriceDeltaMinor: number;
  readonly budgetEffectNetMinor: number;
  readonly budgetEffectVatMinor: number;
  readonly budgetEffectGrossMinor: number;
  readonly createdBy: string | null;
  readonly createdAtUtc: string;
  readonly submittedBy: string | null;
  readonly submittedAtUtc: string | null;
  readonly decidedBy: string | null;
  readonly decidedAtUtc: string | null;
  readonly decisionReason: string | null;
  readonly voidedBy: string | null;
  readonly voidedAtUtc: string | null;
  readonly voidReason: string | null;
  /** For an approved order: a later approved order that must be voided first. */
  readonly voidBlockedBy: string | null;
  readonly lines: readonly ChangeOrderLine[];
  readonly version: number;
  /** Added work on a government project against the agency's limit (a warning, never a block). */
  readonly governmentLimit: GovernmentLimit;
}

/**
 * `applies` only for a government client with a locked budget: `limitMinor` is 10% of the original contract price and `usedMinor`
 * the approved price increases plus this order's own while it waits for approval. Worked out by the server.
 */
export interface GovernmentLimit {
  readonly applies: boolean;
  readonly limitMinor: number;
  readonly usedMinor: number;
  readonly exceeds: boolean;
}

/** The header a draft is created or edited with. */
export interface ChangeOrderDetails {
  readonly title: string;
  readonly description: string | null;
  readonly reason: ChangeOrderReason;
  readonly contractPriceDeltaMinor: number;
}

/** One line as the dialog sends it. Add sends the budget line fields; Revise sends the line, quantity and unit cost; Omit the line. */
export interface ChangeOrderLineFields {
  readonly kind: ChangeOrderLineKind;
  readonly budgetLineId?: string | null;
  readonly subProjectId?: string | null;
  readonly division?: number | null;
  readonly mainScope?: string | null;
  readonly subScope?: string | null;
  readonly wbsCode?: string | null;
  readonly description?: string;
  readonly itemType?: ItemType;
  readonly quantity?: number;
  readonly unit?: string;
  readonly unitCostMinor?: number;
  readonly vatTreatment?: VatTreatment;
  readonly entryDate?: string;
  readonly remarks?: string | null;
}
