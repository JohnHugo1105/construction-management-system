import { VarianceBasis } from '../progress/progress.models';

// The shapes of the schedule API (docs/api/schedule.md). Money is whole minor units (centavos) on the company's comparison basis;
// percentages are basis points (10,000 is 100%); dates are business dates ("2026-11-02"). The server works out every weight, percent,
// planned value and slip; the client draws them and never recalculates.

export interface ScheduleActivity {
  readonly id: string;
  readonly name: string;
  readonly plannedStart: string;
  readonly plannedFinish: string;
  /** Both ends counted. */
  readonly plannedDays: number;
  readonly baselineStart: string | null;
  readonly baselineFinish: string | null;
  /** Planned finish minus baseline finish: positive is later than the baseline. Null without a baseline. */
  readonly slipDays: number | null;
  readonly sortOrder: number;
  /** The linked lines' budget. */
  readonly weightMinor: number;
  /** The budget-weighted percent of the linked lines; null with none linked. */
  readonly percentCompleteBps: number | null;
  /** Its planned finish is before today and it is below 100%. */
  readonly isBehind: boolean;
  /** No budget line is linked: it counts for nothing in the plan. */
  readonly needsLines: boolean;
  readonly budgetLineIds: readonly string[];
  readonly version: number;
}

/** An active budget line, for the picker and to name an activity's lines. */
export interface ScheduleBudgetLine {
  readonly id: string;
  readonly description: string;
  readonly wbsCode: string | null;
  readonly division: number | null;
  readonly divisionName: string;
  readonly budgetMinor: number;
  readonly percentBps: number;
  /** The activity that links it, or null. */
  readonly activityId: string | null;
}

export interface Schedule {
  readonly projectId: string;
  readonly today: string;
  readonly basis: VarianceBasis;
  readonly projectArchived: boolean;
  /** At least one activity has weight: the Progress tab's planned value comes from this schedule. */
  readonly usesSchedule: boolean;
  readonly plannedMinor: number;
  readonly baselinePlannedMinor: number | null;
  readonly planWeightMinor: number;
  readonly budgetMinor: number;
  readonly unplannedBudgetMinor: number;
  readonly unplannedLineCount: number;
  readonly baselineSetAtUtc: string | null;
  readonly activities: readonly ScheduleActivity[];
  readonly budgetLines: readonly ScheduleBudgetLine[];
}

export interface ScheduleCurvePoint {
  readonly date: string;
  readonly isToday: boolean;
  readonly plannedMinor: number;
  readonly baselinePlannedMinor: number | null;
  /** Null after today. */
  readonly earnedMinor: number | null;
  /** Posted cost; null after today. */
  readonly actualMinor: number | null;
}

export interface ScheduleCurve {
  readonly projectId: string;
  readonly today: string;
  readonly basis: VarianceBasis;
  readonly hasBaseline: boolean;
  readonly stepDays: number;
  readonly points: readonly ScheduleCurvePoint[];
}

export interface ActivityDetails {
  readonly name: string;
  readonly plannedStart: string;
  readonly plannedFinish: string;
  readonly budgetLineIds: readonly string[];
}
