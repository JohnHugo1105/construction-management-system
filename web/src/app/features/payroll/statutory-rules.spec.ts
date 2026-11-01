import { statutory, manual } from './payroll.fixtures';
import {
  deductionSource,
  methodsFor,
  needsSetupLines,
  rowRange,
  rowShares,
} from './statutory-rules';
import { StatutoryRow } from './statutory.models';

// Artificial figures only: nothing here is a legal rate.
const row = (overrides: Partial<StatutoryRow> = {}): StatutoryRow => ({
  fromMinor: 500000,
  toMinor: 1000000,
  employeeMinor: 11100,
  employerMinor: 22200,
  baseTaxMinor: 0,
  rateBps: 0,
  ...overrides,
});

describe('statutory rules', () => {
  it('words where a deduction came from', () => {
    expect(deductionSource(statutory('a', 'Sss', 100))).toBe('SSS, calculated');
    expect(deductionSource(statutory('a', 'PagIbig', 100, { isAdjusted: true }))).toBe(
      'Pag-IBIG, adjusted',
    );
    expect(deductionSource(statutory('a', 'WithholdingTax', 0, { isLimited: true }))).toBe(
      'Withholding tax, limited to the pay',
    );
    expect(deductionSource(manual('m', 'Cash advance', 100))).toBeNull();
  });

  it('offers tax brackets only for tax, and brackets or a percentage for contributions', () => {
    expect(methodsFor('WithholdingTax')).toEqual(['TaxBrackets']);
    expect(methodsFor('PhilHealth')).toEqual(['Brackets', 'Percentage']);
  });

  it('turns each agency that needs setup into one line with its fallback', () => {
    expect(needsSetupLines(null)).toEqual([]);
    expect(
      needsSetupLines({
        today: '2026-11-01',
        agencies: [
          {
            agency: 'Sss',
            label: 'SSS',
            state: 'Available',
            message: 'Worked out.',
            fallback: null,
            scheduleId: 't',
            scheduleName: 'x',
            effectiveFrom: '2026-01-01',
          },
          {
            agency: 'PagIbig',
            label: 'Pag-IBIG',
            state: 'NeedsSetup',
            message:
              'Pag-IBIG contributions were not calculated because no Pag-IBIG table is set up.',
            fallback: 'Enter them as deductions, or ask an accountant to set the table up.',
            scheduleId: null,
            scheduleName: null,
            effectiveFrom: null,
          },
        ],
      }),
    ).toEqual([
      'Pag-IBIG contributions were not calculated because no Pag-IBIG table is set up. Enter them as deductions, or ask an accountant to set the table up.',
    ]);
  });

  it('words a row’s range and what it takes', () => {
    expect(rowRange(row(), 'PHP')).toBe('₱5,000.00 to under ₱10,000.00');
    expect(rowRange(row({ toMinor: null }), 'PHP')).toBe('₱5,000.00 and above');
    expect(rowShares(row(), 'Brackets', 'PHP')).toBe('Employee ₱111.00 · employer ₱222.00');
    expect(rowShares(row({ baseTaxMinor: 10000, rateBps: 1500 }), 'TaxBrackets', 'PHP')).toBe(
      '₱100.00 plus 15% of the amount above ₱5,000.00',
    );
    expect(rowShares(row({ rateBps: 1000 }), 'TaxBrackets', 'PHP')).toBe(
      '10% of the amount above ₱5,000.00',
    );
    expect(rowShares(row(), 'TaxBrackets', 'PHP')).toBe('No tax');
  });
});
