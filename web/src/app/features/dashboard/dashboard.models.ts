import {
  CommitmentPosition,
  Health,
  ProgressForecast,
  VarianceBasis,
} from '../costing/costing.models';
import { ProjectStatus } from '../projects/projects.models';

// The dashboard report (`GET /api/reports/dashboard`, docs/api/costing.md). Budget and spent are compared on the basis the
// company chose; the margins always use amounts with VAT because the contract price includes VAT.

/** One project's budget against what it has spent. */
export interface DashboardRow {
  readonly id: string;
  readonly name: string;
  readonly status: ProjectStatus;
  readonly contractPriceMinor: number | null;
  readonly budgetMinor: number;
  readonly actualMinor: number;
  /** Budget minus spent: negative means over budget. */
  readonly remainingMinor: number;
  /** Basis points (10,000 is 100%); null when nothing is budgeted. */
  readonly usedBps: number | null;
  readonly health: Health;
  /** Contract price minus what is spent, or null when the project has no contract price. */
  readonly marginAfterActualMinor: number | null;
  readonly budgetMarginMinor: number | null;
  /** What is promised but not yet invoiced, and the forecast it gives; absent when nothing is committed. */
  readonly commitments?: CommitmentPosition | null;
  /** Where the work done says the project will finish; absent until progress is recorded. Separate from `health`. */
  readonly progressForecast?: ProgressForecast | null;
}

/** The overall row: it adds up every project in the list. */
export interface DashboardTotals {
  readonly projectCount: number;
  readonly contractPriceMinor: number | null;
  readonly budgetMinor: number;
  readonly actualMinor: number;
  readonly remainingMinor: number;
  readonly usedBps: number | null;
  readonly health: Health;
  readonly marginAfterActualMinor: number | null;
  readonly budgetMarginMinor: number | null;
  readonly commitments?: CommitmentPosition | null;
  /** How many of the listed projects are forecast to finish over budget; 0 or absent when none is. */
  readonly forecastOverBudgetCount?: number;
}

export interface Dashboard {
  readonly basis: VarianceBasis;
  readonly projects: readonly DashboardRow[];
  readonly totals: DashboardTotals;
}
