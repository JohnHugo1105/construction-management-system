import { formatDate } from '../../shared/presentation/business-time';
import {
  HolidayType,
  PayRulesFactors,
  PayrollLine,
  PayrollRun,
  PayrollRunListItem,
} from './payroll.models';
import { periodLabel } from './payroll-rules';

export const HOLIDAY_TYPE_LABEL: Record<HolidayType, string> = {
  Regular: 'Regular holiday',
  Special: 'Special day',
};

/** "13th-month pay 2026" for a 13th-month payroll, otherwise the pay period. */
export function runLabel(
  run: Pick<PayrollRunListItem, 'kind' | 'year' | 'periodStart' | 'periodEnd'>,
): string {
  return run.kind === 'ThirteenthMonth' && run.year
    ? `13th-month pay ${run.year}`
    : periodLabel(run.periodStart, run.periodEnd);
}

export function runTitle(run: PayrollRun): string {
  return run.kind === 'ThirteenthMonth' ? runLabel(run) : `Payroll ${runLabel(run)}`;
}

/** The one-line needs-setup note: shown on a payroll only when the company entered no holiday for its year. */
export function holidayNote(run: PayrollRun): string | null {
  const year = run.holidaysMissingForYear;
  return year
    ? `No holidays are entered for ${year}. Add them under Payroll, Holidays if any fall in this period.`
    : null;
}

/** One line on a draft, only when the server counted workers left out of holiday pay because their project is archived. */
export function archivedPostingsNote(run: PayrollRun): string | null {
  const count = run.workersOnArchivedProjectsLeftOut ?? 0;
  if (count <= 0) return null;
  return count === 1
    ? '1 worker posted to an archived project was left out of holiday pay.'
    : `${count} workers posted to archived projects were left out of holiday pay.`;
}

/** "130%", "12.5%". */
export function percentText(bps: number): string {
  return `${Number((bps / 100).toFixed(2))}%`;
}

/** "130" or "12.5" (a percentage typed by a person) as basis points, or null when it is not a number with at most 2 decimals. */
export function bpsFromPercent(text: string): number | null {
  const value = text.trim();
  if (!/^\d{1,4}(\.\d{1,2})?$/.test(value)) return null;
  return Math.round(Number(value) * 100);
}

/** The fields of the pay rules in the order the page shows them, with the words people use. */
export const FACTOR_FIELDS: readonly {
  readonly key: keyof PayRulesFactors;
  readonly label: string;
  readonly hint: string;
  readonly min: number;
  readonly max: number;
}[] = [
  {
    key: 'restDayBps',
    label: 'Rest day',
    hint: 'Pay for an hour on the worker’s rest day.',
    min: 100,
    max: 500,
  },
  {
    key: 'specialDayBps',
    label: 'Special day',
    hint: 'Pay for an hour on a special (non-working) day.',
    min: 100,
    max: 500,
  },
  {
    key: 'specialRestDayBps',
    label: 'Special day on a rest day',
    hint: 'When a special day falls on the worker’s rest day.',
    min: 100,
    max: 500,
  },
  {
    key: 'regularHolidayBps',
    label: 'Regular holiday',
    hint: 'Pay for an hour worked on a regular holiday.',
    min: 100,
    max: 500,
  },
  {
    key: 'regularHolidayRestDayBps',
    label: 'Regular holiday on a rest day',
    hint: 'When a regular holiday falls on the worker’s rest day.',
    min: 100,
    max: 500,
  },
  {
    key: 'premiumDayOvertimeExtraBps',
    label: 'Overtime extra on those days',
    hint: 'Overtime on a rest day or holiday is the day’s rate plus this share.',
    min: 0,
    max: 100,
  },
  {
    key: 'nightDifferentialBps',
    label: 'Night differential',
    hint: 'Added for each night hour, on the day’s rate.',
    min: 0,
    max: 100,
  },
];

/** What a person typed for one factor, checked against the same bounds the server applies (the server has the last word). */
export function checkFactor(field: (typeof FACTOR_FIELDS)[number], text: string): string | null {
  const bps = bpsFromPercent(text);
  if (bps === null) return 'Enter a percentage, for example 130.';
  if (bps < field.min * 100 || bps > field.max * 100)
    return `Enter a percentage between ${field.min}% and ${field.max}%.`;
  return null;
}

/** One row of a worker's folded pay breakdown. */
export interface BreakdownRow {
  readonly label: string;
  readonly amount: string;
  readonly note: string | null;
}

/**
 * How a worker's gross is made up, from the API's figures only: Regular, Overtime and premium, Night, Holiday pay (and 13th-month
 * pay on that payroll). A part that is zero is left out, except the regular pay of an ordinary payroll.
 */
export function payBreakdown(line: PayrollLine, money: (minor: number) => string): BreakdownRow[] {
  const rows: BreakdownRow[] = [];
  const thirteenth = line.thirteenthMonthPayMinor ?? 0;
  if (thirteenth > 0) {
    rows.push({
      label: '13th-month pay',
      amount: money(thirteenth),
      note: `A twelfth of ${money(line.thirteenthMonthBasisMinor ?? 0)} basic pay`,
    });
    return rows;
  }
  rows.push({ label: 'Regular pay', amount: money(line.regularPayMinor), note: null });
  const overtimeAndPremium = line.overtimePayMinor + (line.premiumPayMinor ?? 0);
  if (overtimeAndPremium > 0)
    rows.push({ label: 'Overtime and premium', amount: money(overtimeAndPremium), note: null });
  const night = line.nightPayMinor ?? 0;
  if (night > 0)
    rows.push({
      label: 'Night differential',
      amount: money(night),
      note: line.nightHours ? `${line.nightHours} h at night` : null,
    });
  const holiday = line.holidayPayMinor ?? 0;
  if (holiday > 0)
    rows.push({
      label: 'Holiday pay',
      amount: money(holiday),
      note: `Not worked: ${(line.holidayDates ?? []).map((date) => formatDate(date)).join(', ')}`,
    });
  return rows;
}
