import { payrollExportUrl } from './payroll-api';
import {
  PAYROLL_STATUS,
  checkPeriod,
  daysBetween,
  defaultPeriod,
  periodLabel,
  postingsOf,
  runActions,
} from './payroll-rules';
import { line, run } from './payroll.fixtures';

describe('the pay period', () => {
  it('starts as the 14 days that ended yesterday', () => {
    expect(defaultPeriod('2026-11-01')).toEqual({ start: '2026-10-18', end: '2026-10-31' });
    // Across a year end, and a leap day.
    expect(defaultPeriod('2027-01-05')).toEqual({ start: '2026-12-22', end: '2027-01-04' });
    expect(defaultPeriod('2028-03-05')).toEqual({ start: '2028-02-20', end: '2028-03-04' });
    expect(daysBetween('2026-10-18', '2026-10-31')).toBe(13);
  });

  it('accepts a period that ends today or earlier and lasts at most 31 days', () => {
    expect(checkPeriod('2026-10-18', '2026-10-31', '2026-11-01')).toEqual({});
    expect(checkPeriod('2026-11-01', '2026-11-01', '2026-11-01')).toEqual({});
    expect(checkPeriod('2026-10-01', '2026-11-01', '2026-11-01')).toEqual({});
  });

  it('asks for both days', () => {
    expect(checkPeriod('', '2026-10-31', '2026-11-01').start).toBe(
      'Choose the first day of the pay period.',
    );
    expect(checkPeriod('2026-10-18', '', '2026-11-01').end).toBe(
      'Choose the last day of the pay period.',
    );
  });

  it('refuses a last day before the first, or after today', () => {
    expect(checkPeriod('2026-10-31', '2026-10-18', '2026-11-01').end).toBe(
      "The last day can't be before the first day.",
    );
    expect(checkPeriod('2026-10-18', '2026-11-02', '2026-11-01').end).toBe(
      "The pay period can't end in the future.",
    );
  });

  it('refuses a period longer than 31 days', () => {
    expect(checkPeriod('2026-09-01', '2026-11-01', '2026-11-01').end).toBe(
      'A pay period is at most 31 days.',
    );
  });

  it('words the period the way the activity log and the cost entries do', () => {
    expect(periodLabel('2026-10-18', '2026-10-31')).toBe('18 Oct to 31 Oct 2026');
    expect(periodLabel('2026-12-22', '2027-01-04')).toBe('22 Dec 2026 to 4 Jan 2027');
  });
});

describe('where approving posts the labor cost', () => {
  it('adds each worker’s shares up by project and sub-project, in whole centavos', () => {
    const postings = postingsOf(run());

    expect(postings.map((posting) => [posting.label, posting.amountMinor])).toEqual([
      ['Harbour Annex', 400000],
      ['Riverside Residences', 431250],
      ['Riverside Residences, Tower A', 431250],
    ]);
    // What is posted always adds up to what the run says it pays.
    expect(postings.reduce((sum, posting) => sum + posting.amountMinor, 0)).toBe(
      run().totalGrossMinor,
    );
  });

  it('puts several workers’ shares for one place into one amount', () => {
    const second = line({ workerId: 'w9', grossPayMinor: 100001 });
    const merged = postingsOf(
      run({
        lines: [
          line({ allocations: [{ ...line().allocations[0]!, grossMinor: 333333 }] }),
          { ...second, allocations: [{ ...line().allocations[0]!, grossMinor: 100001 }] },
        ],
      }),
    );

    expect(merged).toHaveLength(1);
    expect(merged[0]!.amountMinor).toBe(433334);
  });

  it('leaves out a place that would be charged nothing, as the server does', () => {
    const postings = postingsOf(
      run({ lines: [line({ allocations: [{ ...line().allocations[0]!, grossMinor: 0 }] })] }),
    );
    expect(postings).toEqual([]);
  });
});

describe('which actions a payroll offers', () => {
  const preparer = { canRun: true, canApprove: false };
  const approver = { canRun: true, canApprove: true };
  const reader = { canRun: false, canApprove: false };

  it('lets a preparer enter deductions and void a draft, and tells them who approves', () => {
    expect(runActions('Draft', preparer)).toEqual({
      editDeductions: true,
      approve: false,
      void: true,
      exportRegister: false,
      approverNote: true,
    });
  });

  it('lets an approver do everything to a draft', () => {
    expect(runActions('Draft', approver)).toEqual({
      editDeductions: true,
      approve: true,
      void: true,
      exportRegister: false,
      approverNote: false,
    });
  });

  it('lets only an approver void an approved payroll, and offers the register to anyone who reads', () => {
    expect(runActions('Approved', approver)).toEqual({
      editDeductions: false,
      approve: false,
      void: true,
      exportRegister: true,
      approverNote: false,
    });
    expect(runActions('Approved', preparer).void).toBe(false);
    expect(runActions('Approved', reader).exportRegister).toBe(true);
  });

  it('offers nothing on a voided payroll', () => {
    for (const rights of [preparer, approver, reader])
      expect(runActions('Voided', rights)).toEqual({
        editDeductions: false,
        approve: false,
        void: false,
        exportRegister: false,
        approverNote: false,
      });
  });

  it('gives a person who may only read no action buttons at all', () => {
    for (const status of ['Draft', 'Approved'] as const) {
      const actions = runActions(status, reader);
      expect(actions.editDeductions || actions.approve || actions.void).toBe(false);
    }
  });
});

describe('payroll words and links', () => {
  it('words each state once, with an icon, for the list and the page', () => {
    expect(Object.values(PAYROLL_STATUS).map((status) => status.label)).toEqual([
      'Draft',
      'Approved',
      'Voided',
    ]);
  });

  it('links the register to the export endpoint of that run, with no pay in the address', () => {
    expect(payrollExportUrl('44fd353b-1d2f-4a7e-947e-f87a6415e2ca')).toBe(
      '/api/payroll-runs/44fd353b-1d2f-4a7e-947e-f87a6415e2ca/export',
    );
    expect(payrollExportUrl('a b/c')).toBe('/api/payroll-runs/a%20b%2Fc/export');
  });
});
