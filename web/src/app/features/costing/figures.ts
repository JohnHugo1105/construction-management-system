import { formatMoney } from '../../shared/presentation/money';
import { formatBps } from '../../shared/presentation/percent';

/** The "Remaining" amount in words: a negative amount is shown as how far over the budget it is. */
export interface RemainingText {
  readonly text: string;
  readonly over: boolean;
}

export function describeRemaining(remainingMinor: number, currency: string): RemainingText {
  return remainingMinor < 0
    ? { text: `Over by ${formatMoney(-remainingMinor, currency)}`, over: true }
    : { text: formatMoney(remainingMinor, currency), over: false };
}

/** The one plain line that says which amounts budget and spent are compared on. */
export function describeBasis(basis: string): string {
  return basis === 'ExcludingVat' ? 'Amounts exclude VAT' : 'Amounts include VAT';
}

/** "40.26%", or an en dash when nothing is budgeted to compare with. */
export function describeUsed(usedBps: number | null): string {
  return usedBps === null ? '–' : formatBps(usedBps);
}

/** The width of a progress bar in percent: never more than a full bar, whatever the overrun. */
export function barWidthPercent(usedBps: number | null): number {
  if (usedBps === null || usedBps <= 0) return 0;
  return Math.min(100, usedBps / 100);
}
