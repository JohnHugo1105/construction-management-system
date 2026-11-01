import { formatMoney } from '../../shared/presentation/money';
import { formatBps } from '../../shared/presentation/percent';
import { PayrollDeduction, StatutoryAgency } from './payroll.models';
import {
  StatutoryMethod,
  StatutoryPeriodBasis,
  StatutoryRow,
  StatutoryStatus,
} from './statutory.models';

/** The agencies in the order deductions are taken from pay. */
export const AGENCIES: readonly StatutoryAgency[] = [
  'Sss',
  'PhilHealth',
  'PagIbig',
  'WithholdingTax',
];

export const AGENCY_LABEL: Record<StatutoryAgency, string> = {
  Sss: 'SSS',
  PhilHealth: 'PhilHealth',
  PagIbig: 'Pag-IBIG',
  WithholdingTax: 'Withholding tax',
};

export const METHOD_LABEL: Record<StatutoryMethod, string> = {
  Brackets: 'Brackets',
  Percentage: 'A percentage of pay',
  TaxBrackets: 'Tax brackets',
};

export const METHOD_HINT: Record<StatutoryMethod, string> = {
  Brackets: 'Pay ranges, each with a fixed employee and employer amount.',
  Percentage: 'A rate of pay for each share, with a lowest and highest pay it applies to.',
  TaxBrackets: 'Pay ranges, each with a base tax and a rate on the amount above the range start.',
};

export const BASIS_LABEL: Record<StatutoryPeriodBasis, string> = {
  Monthly: 'Per month',
  PerRun: 'Per pay run',
};

/** Tax uses tax brackets; contributions use brackets or a percentage. */
export function methodsFor(agency: StatutoryAgency): readonly StatutoryMethod[] {
  return agency === 'WithholdingTax' ? ['TaxBrackets'] : ['Brackets', 'Percentage'];
}

/** Where a deduction came from, in a few words: "SSS, calculated", "PhilHealth, adjusted". Null for one typed by a person. */
export function deductionSource(deduction: PayrollDeduction): string | null {
  if (deduction.source !== 'Statutory' || !deduction.agency) return null;
  const agency = AGENCY_LABEL[deduction.agency];
  if (deduction.isAdjusted) return `${agency}, adjusted`;
  if (deduction.isLimited) return `${agency}, limited to the pay`;
  return `${agency}, calculated`;
}

/**
 * One quiet line for each agency whose amounts can't be worked out yet, in the API's own words with what to do instead.
 * Nothing when every agency is set up.
 */
export function needsSetupLines(status: StatutoryStatus | null): readonly string[] {
  if (!status) return [];
  return status.agencies
    .filter((agency) => agency.state === 'NeedsSetup')
    .map((agency) => (agency.fallback ? `${agency.message} ${agency.fallback}` : agency.message));
}

/** "₱0.00 to under ₱10,000.00", or "₱20,000.00 and above" for the open last row. */
export function rowRange(
  row: Pick<StatutoryRow, 'fromMinor' | 'toMinor'>,
  currency: string,
): string {
  const from = formatMoney(row.fromMinor, currency);
  return row.toMinor === null
    ? `${from} and above`
    : `${from} to under ${formatMoney(row.toMinor, currency)}`;
}

/** What a row takes, in words: "Employee ₱111.00 · employer ₱222.00", or "₱100.00 plus 15% of the amount above ₱5,000.00". */
export function rowShares(row: StatutoryRow, method: StatutoryMethod, currency: string): string {
  if (method === 'TaxBrackets') {
    const rate =
      row.rateBps === 0
        ? ''
        : `${formatBps(row.rateBps)} of the amount above ${formatMoney(row.fromMinor, currency)}`;
    if (row.baseTaxMinor === 0)
      return rate === '' ? 'No tax' : rate.charAt(0).toUpperCase() + rate.slice(1);
    return rate === ''
      ? formatMoney(row.baseTaxMinor, currency)
      : `${formatMoney(row.baseTaxMinor, currency)} plus ${rate}`;
  }
  return `Employee ${formatMoney(row.employeeMinor, currency)} · employer ${formatMoney(row.employerMinor, currency)}`;
}
