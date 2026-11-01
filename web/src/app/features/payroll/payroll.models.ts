/** A payroll run's state, as the API writes it. */
export type PayrollStatus = 'Draft' | 'Approved' | 'Voided';

/** One row of the payroll list. Amounts are minor units (centavos). */
export interface PayrollRunListItem {
  readonly id: string;
  /** A business date, "2026-11-01". */
  readonly periodStart: string;
  readonly periodEnd: string;
  readonly status: PayrollStatus;
  readonly workerCount: number;
  readonly totalGrossMinor: number;
  readonly totalDeductionsMinor: number;
  readonly totalNetMinor: number;
  readonly createdAtUtc: string;
  readonly approvedAtUtc: string | null;
  readonly version: number;
}

export interface PayrollDeduction {
  readonly id: string;
  readonly description: string;
  readonly amountMinor: number;
}

/** The part of one worker's pay that belongs to one project (and sub-project). The shares add up to the line's gross pay. */
export interface PayrollAllocation {
  readonly projectId: string;
  readonly projectName: string;
  readonly subProjectId: string | null;
  readonly subProjectName: string | null;
  readonly regularHours: number;
  readonly overtimeHours: number;
  readonly grossMinor: number;
}

/** One worker's pay in a run. Every figure comes from the API. */
export interface PayrollLine {
  readonly workerId: string;
  readonly workerName: string;
  readonly employeeNumber: string;
  readonly designation: string;
  readonly regularHours: number;
  readonly overtimeHours: number;
  readonly regularPayMinor: number;
  readonly overtimePayMinor: number;
  readonly grossPayMinor: number;
  readonly deductionsMinor: number;
  readonly netPayMinor: number;
  readonly deductions: readonly PayrollDeduction[];
  readonly allocations: readonly PayrollAllocation[];
}

/** A whole run. Every change sends back the `version` the person opened. */
export interface PayrollRun {
  readonly id: string;
  readonly periodStart: string;
  readonly periodEnd: string;
  readonly status: PayrollStatus;
  readonly workerCount: number;
  readonly totalGrossMinor: number;
  readonly totalDeductionsMinor: number;
  readonly totalNetMinor: number;
  readonly hoursPerDay: number;
  readonly overtimeMultiplierBps: number;
  readonly createdByName: string | null;
  readonly createdAtUtc: string;
  readonly approvedByName: string | null;
  readonly approvedAtUtc: string | null;
  readonly voidedByName: string | null;
  readonly voidedAtUtc: string | null;
  readonly voidReason: string | null;
  readonly lines: readonly PayrollLine[];
  readonly version: number;
}

export interface CreateRunRequest {
  readonly periodStart: string;
  readonly periodEnd: string;
  readonly clientRequestId: string;
}

export interface DeductionInput {
  readonly description: string;
  readonly amountMinor: number;
}

export interface PayrollListParams {
  readonly page: number;
  readonly pageSize: number;
}
