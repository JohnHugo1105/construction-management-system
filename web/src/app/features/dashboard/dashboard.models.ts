import { Health, VarianceBasis } from '../costing/costing.models';
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
}

export interface Dashboard {
  readonly basis: VarianceBasis;
  readonly projects: readonly DashboardRow[];
  readonly totals: DashboardTotals;
}
