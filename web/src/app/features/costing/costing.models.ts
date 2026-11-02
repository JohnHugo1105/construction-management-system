import { PagedResult } from '../../core/api/paged-result';

// The shapes of the costing API (docs/api/costing.md). Money is whole minor units (centavos); quantities are decimals with up to
// four places; dates are business dates ("2026-11-02"). The client displays these as they arrive and never recalculates them.

export type ItemType = 'Material' | 'Labor' | 'Equipment' | 'Subcontract' | 'Other';

export type VatTreatment = 'Inclusive' | 'Exclusive' | 'Exempt';

export type CostOrigin = 'Manual' | 'Payroll' | 'Inventory' | 'Subcontract';

/** How a slice of the budget stands: the API works it out from exact amounts. */
export type Health = 'NoActivity' | 'OnTrack' | 'NearLimit' | 'OverBudget' | 'Unbudgeted';

export type VarianceBasis = 'IncludingVat' | 'ExcludingVat';

/** Money added up over a whole filtered set (not only the page on screen). */
export interface MoneyTotals {
  readonly netMinor: number;
  readonly vatMinor: number;
  readonly grossMinor: number;
}

/** A page of rows plus the totals of the whole filtered set. */
export interface TotaledPage<T> extends PagedResult<T> {
  readonly totals: MoneyTotals;
}

/** What a budget line and a cost entry have in common. */
interface LineBase {
  readonly id: string;
  readonly projectId: string;
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
  readonly netMinor: number;
  readonly vatMinor: number;
  readonly grossMinor: number;
  readonly entryDate: string;
  readonly remarks: string | null;
  readonly createdAtUtc: string;
  readonly version: number;
}

/** What a project plans to spend on one item. */
export interface BudgetLine extends LineBase {
  readonly status: 'Active' | 'Removed';
  readonly removedAtUtc: string | null;
  readonly removedReason: string | null;
  /** The change order that added this line ("CO-001"), when one did. */
  readonly changeOrderId?: string | null;
  readonly changeOrderNumber?: string | null;
}

/** What a project has spent. System-made entries (`origin` Payroll or Inventory) have `canEdit` false. */
export interface CostEntry extends LineBase {
  readonly origin: CostOrigin;
  readonly originReferenceId: string | null;
  readonly sourceDealer: string;
  readonly invoiceNumber: string | null;
  readonly status: 'Posted' | 'Voided';
  readonly voidedAtUtc: string | null;
  readonly voidReason: string | null;
  readonly canEdit: boolean;
  /** The purchase order line this invoice is matched to, and that order's number for display. */
  readonly purchaseOrderLineId?: string | null;
  readonly purchaseOrderNumber?: string | null;
}

// ---- What the forms send ---------------------------------------------------------------------------------------------------

/** The fields a budget line and a cost entry are written with. The VAT rate is not one of them: the server keeps its own. */
export interface LineFields {
  readonly subProjectId: string | null;
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
  readonly entryDate: string;
  readonly remarks: string | null;
}

export interface CostEntryFields extends LineFields {
  readonly sourceDealer: string;
  readonly invoiceNumber: string;
  /** The approved purchase order line this invoice is for; left out when there is none (an edit then clears the match). */
  readonly purchaseOrderLineId?: string;
}

// ---- Lists -----------------------------------------------------------------------------------------------------------------

export interface BudgetLineFilters {
  readonly subProjectId: string;
  /** 1 to 16, or 0 for every division. */
  readonly division: number;
  readonly itemType: string;
  readonly search: string;
  readonly includeRemoved: boolean;
}

export interface CostEntryFilters {
  readonly subProjectId: string;
  readonly division: number;
  readonly itemType: string;
  readonly origin: string;
  readonly from: string;
  readonly to: string;
  readonly search: string;
  readonly includeVoided: boolean;
}

export interface PageParams {
  readonly page: number;
  readonly pageSize: number;
}

// ---- Reference data --------------------------------------------------------------------------------------------------------

export interface WbsScope {
  readonly code: string;
  readonly name: string;
}

export interface WbsDivision {
  readonly number: number;
  readonly mainScope: string;
  readonly scopes: readonly WbsScope[];
}

export interface ItemTypeChoice {
  readonly value: ItemType;
  readonly label: string;
}

export interface VatTreatmentChoice {
  readonly value: VatTreatment;
  readonly label: string;
  readonly hint: string;
}

/** The choices the forms offer, as `GET /api/costing/reference` returns them. */
export interface CostingReference {
  readonly divisions: readonly WbsDivision[];
  readonly units: readonly string[];
  readonly itemTypes: readonly ItemTypeChoice[];
  readonly vatTreatments: readonly VatTreatmentChoice[];
  readonly vatRateBps: number;
}

// ---- Cost summary ----------------------------------------------------------------------------------------------------------

/** Budget against actual for one slice, on the company's comparison basis. */
export interface Figures {
  readonly budgetMinor: number;
  readonly actualMinor: number;
  /** Budget minus actual: negative means over budget. */
  readonly remainingMinor: number;
  /** Basis points (10,000 is 100%); null when nothing is budgeted. */
  readonly usedBps: number | null;
  readonly health: Health;
}

export interface WbsSummary extends Figures {
  readonly wbsCode: string | null;
  readonly name: string;
}

export interface DivisionSummary extends Figures {
  /** Null for lines with no classification (named "Unclassified"). */
  readonly division: number | null;
  readonly name: string;
  readonly wbs: readonly WbsSummary[];
}

export interface SubProjectSummary extends Figures {
  /** Null for budget and cost that belong to the whole project. */
  readonly subProjectId: string | null;
  readonly name: string;
}

export interface ItemTypeSummary extends Figures {
  readonly itemType: ItemType;
  readonly label: string;
}

/**
 * Money promised but not yet invoiced (open purchase orders and subcontracts) and the forecast it gives: actual plus committed,
 * judged against the budget. Absent when nothing is committed, so a project without orders shows no empty figures.
 */
export interface CommitmentPosition {
  readonly committedMinor: number;
  readonly forecastMinor: number;
  /** Budget minus forecast: negative means the forecast is over budget. */
  readonly forecastRemainingMinor: number;
  readonly forecastHealth: Health;
}

/**
 * Where the work done says the project will finish: the Progress tab's forecast at completion, on the company's comparison basis.
 * Absent until progress is recorded. It is a separate signal from health, which only compares money spent with the budget.
 */
export interface ProgressForecast {
  readonly estimateAtCompletionMinor: number;
  /** Forecast minus budget: zero or negative means the project is forecast to finish within its budget. */
  readonly overBudgetMinor: number;
  /** The work done as a share of the budget, in basis points. */
  readonly percentCompleteBps: number;
}

/** True when the forecast is above the budget, the only case the dashboard and the Budget tab flag. */
export function isForecastOver(forecast: ProgressForecast | null | undefined): boolean {
  return forecast !== null && forecast !== undefined && forecast.overBudgetMinor > 0;
}

export interface CostSummary {
  readonly projectId: string;
  readonly projectName: string;
  readonly projectStatus: string;
  readonly basis: VarianceBasis;
  readonly contractPriceMinor: number | null;
  readonly position: Figures;
  readonly marginAfterActualMinor: number | null;
  readonly budgetMarginMinor: number | null;
  readonly byDivision: readonly DivisionSummary[];
  readonly bySubProject: readonly SubProjectSummary[];
  readonly byItemType: readonly ItemTypeSummary[];
  readonly commitments?: CommitmentPosition | null;
  readonly progressForecast?: ProgressForecast | null;
}
