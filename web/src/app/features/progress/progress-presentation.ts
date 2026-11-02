import { formatMoney } from '../../shared/presentation/money';
import { EarnedValue, PlannedFrom } from './progress.models';

// Plain-words sentences for the figures the server worked out. Nothing here calculates earned value: it only turns the numbers
// the API sent into the words a project manager would use.

export type Tone = 'good' | 'warning' | 'neutral';

export interface Sentence {
  readonly text: string;
  readonly tone: Tone;
}

/** A share in basis points as a whole percent: 4706 gives "47%". Nothing (not enough to say) gives an empty string. */
export function wholePercent(bps: number | null): string {
  return bps === null ? '' : `${Math.round(bps / 100)}%`;
}

/** "47% complete", or nothing when the project has no budget to measure against. */
export function completeHeadline(value: EarnedValue): string {
  return value.percentCompleteBps === null ? '' : `${wholePercent(value.percentCompleteBps)} complete`;
}

/** "Spent ₱7,210,000.00 for work worth ₱6,070,800.00". */
export function spentForWorth(value: EarnedValue, currency: string): Sentence {
  const text = `Spent ${formatMoney(value.spentMinor, currency)} for work worth ${formatMoney(value.earnedMinor, currency)}`;
  return { text, tone: value.earnedMinor >= value.spentMinor ? 'good' : 'warning' };
}

/** What each peso spent is earning, said the way a person counts: "Each peso spent is earning 84 centavos of work." */
export function costIndexSentence(cpiBps: number | null, spentMinor: number): Sentence | null {
  if (cpiBps === null) return null;
  if (cpiBps === 0 && spentMinor > 0)
    return { text: 'Money has been spent, but no work is recorded against it yet.', tone: 'warning' };
  const centavos = Math.round(cpiBps / 100);
  if (centavos >= 100) {
    const pesos = (centavos / 100).toFixed(2);
    return { text: `Each peso spent is earning ₱${pesos} of work.`, tone: 'good' };
  }
  return {
    text: `Each peso spent is earning ${centavos} centavo${centavos === 1 ? '' : 's'} of work.`,
    tone: 'warning',
  };
}

/**
 * Where the work stands against the plan for today. Nothing when there is no plan to measure against. When the planned value
 * follows the project's schedule, the sentence says so, so nobody mistakes it for the straight line between the project's dates.
 */
export function scheduleSentence(spiBps: number | null, plannedFrom: PlannedFrom | null = null): Sentence | null {
  if (spiBps === null) return null;
  const share = wholePercent(spiBps);
  const bySchedule = plannedFrom === 'Schedule';
  const plan = bySchedule ? 'the schedule planned' : 'was planned';
  if (spiBps === 10_000)
    return {
      text: bySchedule
        ? 'On schedule: the work done matches what the schedule planned for today.'
        : 'On schedule: the work done matches the plan for today.',
      tone: 'good',
    };
  return spiBps > 10_000
    ? { text: `Ahead of schedule: the work done is ${share} of what ${plan} by today.`, tone: 'good' }
    : { text: `Behind schedule: the work done is ${share} of what ${plan} by today.`, tone: 'warning' };
}

/** "Forecast to finish at ₱21,270,000.00, which is ₱3,360,000.00 over the budget." */
export function forecastSentence(value: EarnedValue, currency: string): Sentence {
  const forecast = formatMoney(value.forecastMinor, currency);
  const difference = value.forecastVarianceMinor;
  if (difference === 0) return { text: `Forecast to finish at ${forecast}, right on the budget.`, tone: 'good' };
  const amount = formatMoney(Math.abs(difference), currency);
  return difference < 0
    ? { text: `Forecast to finish at ${forecast}, which is ${amount} over the budget.`, tone: 'warning' }
    : { text: `Forecast to finish at ${forecast}, which is ${amount} under the budget.`, tone: 'good' };
}

/** The forecast margin as money and a tone. Null when the project has no contract price. */
export function forecastMargin(
  value: EarnedValue,
  currency: string,
): { readonly text: string; readonly negative: boolean } | null {
  if (value.forecastMarginMinor === null) return null;
  return {
    text: formatMoney(value.forecastMarginMinor, currency),
    negative: value.forecastMarginMinor < 0,
  };
}
