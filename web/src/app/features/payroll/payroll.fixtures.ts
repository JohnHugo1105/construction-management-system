import { PayrollDeduction, PayrollLine, PayrollRun, PayrollRunListItem } from './payroll.models';

/** A deduction typed by a person. */
export function manual(id: string, description: string, amountMinor: number): PayrollDeduction {
  return {
    id,
    description,
    amountMinor,
    source: 'Manual',
    agency: null,
    employerShareMinor: 0,
    calculatedMinor: null,
    isAdjusted: false,
    isLimited: false,
    adjustmentReason: null,
  };
}

/** A deduction worked out from a table (the figures in specs are artificial). */
export function statutory(
  id: string,
  agency: PayrollDeduction['agency'],
  amountMinor: number,
  overrides: Partial<PayrollDeduction> = {},
): PayrollDeduction {
  const descriptions: Record<string, string> = {
    Sss: 'SSS contribution',
    PhilHealth: 'PhilHealth contribution',
    PagIbig: 'Pag-IBIG contribution',
    WithholdingTax: 'Withholding tax',
  };
  return {
    id,
    description: descriptions[agency ?? 'Sss'] ?? 'Contribution',
    amountMinor,
    source: 'Statutory',
    agency,
    employerShareMinor: 0,
    calculatedMinor: amountMinor,
    isAdjusted: false,
    isLimited: false,
    adjustmentReason: null,
    ...overrides,
  };
}

export function line(overrides: Partial<PayrollLine> = {}): PayrollLine {
  return {
    workerId: 'w1',
    workerName: 'Juan Dela Cruz',
    employeeNumber: 'W-001',
    designation: 'Foreman',
    regularHours: 80,
    overtimeHours: 4,
    regularPayMinor: 800000,
    overtimePayMinor: 62500,
    grossPayMinor: 862500,
    deductionsMinor: 0,
    netPayMinor: 862500,
    deductions: [],
    employerSharesMinor: 0,
    statutoryLimited: false,
    allocations: [
      {
        projectId: 'p1',
        projectName: 'Riverside Residences',
        subProjectId: null,
        subProjectName: null,
        regularHours: 40,
        overtimeHours: 4,
        grossMinor: 431250,
      },
      {
        projectId: 'p1',
        projectName: 'Riverside Residences',
        subProjectId: 's1',
        subProjectName: 'Tower A',
        regularHours: 40,
        overtimeHours: 0,
        grossMinor: 431250,
      },
    ],
    ...overrides,
  };
}

/** A run with two workers whose pay is charged to two projects. The totals are the API's, written out by hand. */
export function run(overrides: Partial<PayrollRun> = {}): PayrollRun {
  return {
    id: 'run1',
    periodStart: '2026-10-18',
    periodEnd: '2026-10-31',
    status: 'Draft',
    workerCount: 2,
    totalGrossMinor: 1262500,
    totalDeductionsMinor: 15000,
    totalNetMinor: 1247500,
    hoursPerDay: 8,
    overtimeMultiplierBps: 12500,
    createdByName: 'Hannah HR-Officer',
    createdAtUtc: '2026-11-01T08:00:00Z',
    approvedByName: null,
    approvedAtUtc: null,
    voidedByName: null,
    voidedAtUtc: null,
    voidReason: null,
    lines: [
      line(),
      line({
        workerId: 'w2',
        workerName: 'Pedro Reyes',
        employeeNumber: 'W-002',
        designation: 'Mason',
        regularHours: 40,
        overtimeHours: 0,
        regularPayMinor: 400000,
        overtimePayMinor: 0,
        grossPayMinor: 400000,
        deductionsMinor: 15000,
        netPayMinor: 385000,
        deductions: [manual('d1', 'SSS contribution', 15000)],
        allocations: [
          {
            projectId: 'p2',
            projectName: 'Harbour Annex',
            subProjectId: null,
            subProjectName: null,
            regularHours: 40,
            overtimeHours: 0,
            grossMinor: 400000,
          },
        ],
      }),
    ],
    version: 2,
    runsPerMonth: 2,
    totalEmployerSharesMinor: 0,
    ...overrides,
  };
}

export function listRun(overrides: Partial<PayrollRunListItem> = {}): PayrollRunListItem {
  return {
    id: 'run1',
    periodStart: '2026-10-18',
    periodEnd: '2026-10-31',
    status: 'Draft',
    workerCount: 2,
    totalGrossMinor: 1262500,
    totalDeductionsMinor: 15000,
    totalNetMinor: 1247500,
    createdAtUtc: '2026-11-01T08:00:00Z',
    approvedAtUtc: null,
    version: 2,
    ...overrides,
  };
}
