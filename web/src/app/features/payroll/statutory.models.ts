import { StatutoryAgency } from './payroll.models';

export type StatutoryMethod = 'Brackets' | 'Percentage' | 'TaxBrackets';
export type StatutoryPeriodBasis = 'Monthly' | 'PerRun';
export type StatutoryScheduleStatus = 'Active' | 'Retired';

/** Whether an agency's amounts are worked out for you (`Available`) or must be entered by hand (`NeedsSetup`). */
export interface StatutoryAgencyStatus {
  readonly agency: StatutoryAgency;
  readonly label: string;
  readonly state: 'Available' | 'NeedsSetup';
  /** Plain words from the API, ready to show. */
  readonly message: string;
  readonly fallback: string | null;
  readonly scheduleId: string | null;
  readonly scheduleName: string | null;
  readonly effectiveFrom: string | null;
}

export interface StatutoryStatus {
  readonly today: string;
  readonly agencies: readonly StatutoryAgencyStatus[];
}

export interface StatutoryScheduleListItem {
  readonly id: string;
  readonly agency: StatutoryAgency;
  readonly agencyLabel: string;
  readonly name: string;
  readonly effectiveFrom: string;
  readonly status: StatutoryScheduleStatus;
  readonly method: StatutoryMethod;
  readonly periodBasis: StatutoryPeriodBasis;
  readonly rowCount: number;
  /** The table that applies today. */
  readonly inForce: boolean;
  readonly version: number;
}

/** One bracket row: from `fromMinor` up to, but not including, `toMinor` (null on the last row). */
export interface StatutoryRow {
  readonly fromMinor: number;
  readonly toMinor: number | null;
  readonly employeeMinor: number;
  readonly employerMinor: number;
  readonly baseTaxMinor: number;
  readonly rateBps: number;
}

export interface StatutorySchedule {
  readonly id: string;
  readonly agency: StatutoryAgency;
  readonly agencyLabel: string;
  readonly name: string;
  readonly effectiveFrom: string;
  readonly status: StatutoryScheduleStatus;
  readonly method: StatutoryMethod;
  readonly periodBasis: StatutoryPeriodBasis;
  readonly employeeRateBps: number | null;
  readonly employerRateBps: number | null;
  readonly baseFloorMinor: number | null;
  readonly baseCeilingMinor: number | null;
  readonly employeeMinimumMinor: number | null;
  readonly employeeMaximumMinor: number | null;
  readonly employerMinimumMinor: number | null;
  readonly employerMaximumMinor: number | null;
  readonly rows: readonly StatutoryRow[];
  /** An approved payroll used this table, so it can't be changed: add a new one. */
  readonly usedByApprovedPayroll: boolean;
  readonly inForce: boolean;
  readonly retiredAtUtc: string | null;
  readonly version: number;
}

export interface SaveScheduleRequest {
  readonly agency: StatutoryAgency;
  readonly name: string;
  readonly effectiveFrom: string;
  readonly method: StatutoryMethod;
  readonly periodBasis: StatutoryPeriodBasis;
  readonly employeeRateBps: number | null;
  readonly employerRateBps: number | null;
  readonly baseFloorMinor: number | null;
  readonly baseCeilingMinor: number | null;
  readonly employeeMinimumMinor: number | null;
  readonly employeeMaximumMinor: number | null;
  readonly employerMinimumMinor: number | null;
  readonly employerMaximumMinor: number | null;
  readonly rows: readonly StatutoryRow[] | null;
  readonly clientRequestId: string | null;
  readonly version: number;
}

/** What one agency is owed for a payroll. */
export interface StatutoryAgencyTotal {
  readonly agency: StatutoryAgency;
  readonly label: string;
  readonly scheduleNames: readonly string[];
  readonly workerCount: number;
  readonly employeeMinor: number;
  readonly employerMinor: number;
  readonly toRemitMinor: number;
  readonly adjustedCount: number;
  readonly limitedCount: number;
}

export interface PayrollStatutory {
  readonly runId: string;
  readonly status: string;
  readonly runsPerMonth: number;
  readonly totalEmployeeMinor: number;
  readonly totalEmployerMinor: number;
  readonly totalToRemitMinor: number;
  readonly agencies: readonly StatutoryAgencyTotal[];
}
