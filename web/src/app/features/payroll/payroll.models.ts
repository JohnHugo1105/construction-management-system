/** A payroll run's state, as the API writes it. */
export type PayrollStatus = 'Draft' | 'Approved' | 'Voided';

/** A pay period's payroll, or the 13th-month pay for a calendar year. */
export type PayrollRunKind = 'Regular' | 'ThirteenthMonth';

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
  readonly kind?: PayrollRunKind;
  /** The year a 13th-month payroll pays; null for a pay period's payroll. */
  readonly year?: number | null;
}

/** The agencies a contribution or tax is paid to, in the order they are taken from pay. */
export type StatutoryAgency = 'Sss' | 'PhilHealth' | 'PagIbig' | 'WithholdingTax';

/**
 * One deduction. A manual one was typed by a person; a statutory one was worked out from its agency's table: `calculatedMinor`
 * is what the table gave, `isAdjusted` says a person changed it, `isLimited` that it was cut to fit the pay, and
 * `employerShareMinor` is what the employer adds (not taken from pay).
 */
export interface PayrollDeduction {
  readonly id: string;
  readonly description: string;
  readonly amountMinor: number;
  readonly source: 'Manual' | 'Statutory';
  readonly agency: StatutoryAgency | null;
  readonly employerShareMinor: number;
  readonly calculatedMinor: number | null;
  readonly isAdjusted: boolean;
  readonly isLimited: boolean;
  readonly adjustmentReason: string | null;
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
  /** What the employer adds to the worker's contributions. */
  readonly employerSharesMinor: number;
  /** A calculated amount was cut because the deductions would have been more than the pay. */
  readonly statutoryLimited: boolean;
  /**
   * The rest of the gross, all from the API: `regularPayMinor` is the plain-rate pay for the regular hours; `premiumPayMinor` what
   * rest days and holidays add to them; `overtimePayMinor` includes the premium on overtime on those days; `nightPayMinor` the night
   * differential; `holidayPayMinor` regular holidays paid without being worked (on `holidayDates`); `thirteenthMonthPayMinor` a
   * 13th-month payroll's amount, a twelfth of `thirteenthMonthBasisMinor`.
   */
  readonly nightHours?: number;
  readonly premiumPayMinor?: number;
  readonly nightPayMinor?: number;
  readonly holidayPayMinor?: number;
  readonly thirteenthMonthPayMinor?: number;
  readonly thirteenthMonthBasisMinor?: number;
  readonly holidayDates?: readonly string[] | null;
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
  /** The company's pay runs a month when the contributions were last worked out. */
  readonly runsPerMonth: number;
  readonly totalEmployerSharesMinor: number;
  readonly kind?: PayrollRunKind;
  /** The year a 13th-month payroll pays (its period is the whole year); null otherwise. */
  readonly year?: number | null;
  /** Set when the company has no holiday entered for the year the period ends in: the run page says so in one line. */
  readonly holidaysMissingForYear?: number | null;
  /** On a draft: workers posted to an archived project on a holiday in the period, left out of holiday pay (the server counts them). */
  readonly workersOnArchivedProjectsLeftOut?: number;
}

/**
 * The company's premium factors, in basis points of the ordinary hourly rate (13,000 = 130%). `overtimeMultiplierBps` is the
 * ordinary-day overtime multiple from the company settings, shown here and changed there. `isDefault` is true (and `version` 0) while
 * the company has not saved its own rules.
 */
export interface PayRules {
  readonly restDayBps: number;
  readonly specialDayBps: number;
  readonly specialRestDayBps: number;
  readonly regularHolidayBps: number;
  readonly regularHolidayRestDayBps: number;
  readonly premiumDayOvertimeExtraBps: number;
  readonly nightDifferentialBps: number;
  readonly overtimeMultiplierBps: number;
  readonly isDefault: boolean;
  readonly version: number;
}

export type PayRulesFactors = Omit<PayRules, 'overtimeMultiplierBps' | 'isDefault' | 'version'>;

export type HolidayType = 'Regular' | 'Special';

/** A holiday on the company calendar. `coveredByApprovedPayroll`: an approved payroll paid that day, so it can't change. */
export interface CompanyHoliday {
  readonly id: string;
  readonly date: string;
  readonly name: string;
  readonly type: HolidayType;
  readonly coveredByApprovedPayroll: boolean;
  readonly version: number;
}

export interface HolidayCalendar {
  readonly year: number;
  readonly holidays: readonly CompanyHoliday[];
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
