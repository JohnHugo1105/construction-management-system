import { PagedResult } from '../../core/api/paged-result';

// The shapes of the progress API (docs/api/progress.md). Money is whole minor units (centavos); percentages and indices are basis
// points (10,000 is 100% or 1.00); dates are business dates ("2026-11-02"). The client displays these as they arrive and never
// recalculates earned value.

export type VarianceBasis = 'IncludingVat' | 'ExcludingVat';

/** What earned value says about the project on the date, worked out by the server. */
export interface EarnedValue {
  readonly budgetMinor: number;
  readonly earnedMinor: number;
  readonly spentMinor: number;
  readonly plannedMinor: number | null;
  readonly costVarianceMinor: number;
  readonly scheduleVarianceMinor: number | null;
  /** Work earned per unit spent; 10,000 is one peso of work for each peso spent. Null when nothing is spent. */
  readonly cpiBps: number | null;
  /** Work earned per unit planned by the date. Null without a target end date. */
  readonly spiBps: number | null;
  readonly forecastMinor: number;
  readonly toCompleteMinor: number;
  /** Budget minus forecast: negative means a forecast overrun. */
  readonly forecastVarianceMinor: number;
  readonly percentCompleteBps: number | null;
  readonly plannedPercentBps: number | null;
  readonly contractPriceMinor: number | null;
  readonly forecastMarginMinor: number | null;
}

export interface ProgressLine {
  readonly budgetLineId: string;
  readonly description: string;
  readonly wbsCode: string | null;
  readonly budgetMinor: number;
  readonly percentBps: number;
  readonly lastUpdatedOn: string | null;
  readonly earnedMinor: number;
}

export interface ProgressDivision {
  /** The division number, or null for lines with no classification. */
  readonly division: number | null;
  readonly name: string;
  readonly budgetMinor: number;
  readonly earnedMinor: number;
  readonly percentBps: number | null;
  readonly lines: readonly ProgressLine[];
}

export interface Progress {
  readonly projectId: string;
  readonly asOfDate: string;
  readonly basis: VarianceBasis;
  /** False until someone has recorded progress on any line. */
  readonly hasProgress: boolean;
  readonly lastUpdatedOn: string | null;
  readonly earnedValue: EarnedValue;
  readonly divisions: readonly ProgressDivision[];
}

export interface ProgressHistoryItem {
  readonly id: string;
  readonly asOfDate: string;
  readonly percentBps: number;
  readonly note: string | null;
  readonly recordedBy: string | null;
  readonly recordedAtUtc: string;
}

export type ProgressHistoryPage = PagedResult<ProgressHistoryItem>;

export interface ProgressLineRequest {
  readonly budgetLineId: string;
  readonly percentBps: number;
  readonly note: string | null;
  /** The percent the person was looking at; a different saved figure makes the server refuse the update as stale. */
  readonly expectedPercentBps: number;
}

export interface RecordProgressRequest {
  readonly asOfDate: string;
  readonly lines: readonly ProgressLineRequest[];
  readonly clientRequestId: string;
}

export interface RecordProgressResult {
  readonly asOfDate: string;
  readonly linesUpdated: number;
  readonly recordedAtUtc: string;
}
