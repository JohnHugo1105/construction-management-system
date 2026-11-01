/** Whether budgets and actual costs are compared with or without VAT. */
export type VarianceBasis = 'IncludingVat' | 'ExcludingVat';

/** The company's settings, as `GET /api/organization` returns them. Rates are in basis points (1,200 is 12%). */
export interface Organization {
  readonly id: string;
  readonly name: string;
  readonly currencyCode: string;
  readonly vatRateBps: number;
  readonly timeZoneId: string;
  /** Paid hours in a working day. */
  readonly hoursPerDay: number;
  /** Overtime pay as a share of the hourly rate, in basis points (12,500 is 125%). */
  readonly overtimeMultiplierBps: number;
  readonly varianceBasis: VarianceBasis;
  /** How many pay runs the company makes in a month (1 to 4). */
  readonly payrollRunsPerMonth: number;
  readonly version: number;
}

export interface UpdateOrganizationRequest {
  readonly name: string;
  readonly currencyCode: string;
  readonly vatRateBps: number;
  readonly timeZoneId: string;
  readonly hoursPerDay: number;
  readonly overtimeMultiplierBps: number;
  readonly varianceBasis: VarianceBasis;
  readonly payrollRunsPerMonth: number;
  readonly version: number;
}
