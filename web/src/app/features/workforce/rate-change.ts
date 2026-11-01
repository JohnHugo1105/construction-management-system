import { addDays } from '../../shared/presentation/business-time';
import { RATE_CATEGORIES, RateCategoryChoice } from './worker-status';
import { RateCategory, WorkerRate } from './workforce.models';

/** The newest line of pay history by effective date (it may start in the future). Null when there is no history to read. */
export function latestRate(rates: readonly WorkerRate[]): WorkerRate | null {
  return rates.reduce<WorkerRate | null>(
    (newest, rate) =>
      newest === null || rate.effectiveFrom > newest.effectiveFrom ? rate : newest,
    null,
  );
}

/** The line of pay history in force on a day: the newest that has started. Null before the first one. */
export function rateInForce(rates: readonly WorkerRate[], today: string): WorkerRate | null {
  return rates.reduce<WorkerRate | null>(
    (found, rate) =>
      rate.effectiveFrom <= today && (found === null || rate.effectiveFrom > found.effectiveFrom)
        ? rate
        : found,
    null,
  );
}

/**
 * The dates a new pay rate may start on. History is never rewritten, so a change must start after the newest one and not before
 * the hire date; today is offered unless that has already passed.
 */
export function rateChangeDates(
  rates: readonly WorkerRate[],
  hireDate: string,
  today: string,
): { readonly earliest: string; readonly suggested: string } {
  const newest = latestRate(rates);
  const afterNewest = newest ? (addDays(newest.effectiveFrom, 1) ?? hireDate) : hireDate;
  const earliest = afterNewest > hireDate ? afterNewest : hireDate;
  return { earliest, suggested: today > earliest ? today : earliest };
}

export function categoryChoice(category: RateCategory | null): RateCategoryChoice | null {
  return RATE_CATEGORIES.find((choice) => choice.value === category) ?? null;
}

/** What the designation field must do for a category: stay as it is, or change. Null while no category is chosen. */
export type DesignationRule = 'keep' | 'change';

export function designationRule(category: RateCategory | null): DesignationRule | null {
  const choice = categoryChoice(category);
  if (!choice) return null;
  return choice.changesDesignation ? 'change' : 'keep';
}

/** The message for a designation that does not suit the category, or null when it does. */
export function designationProblem(
  category: RateCategory | null,
  designation: string,
  current: string,
): string | null {
  const rule = designationRule(category);
  const same = designation.trim().toLowerCase() === current.trim().toLowerCase();
  if (rule === 'change' && designation.trim() !== '' && same)
    return 'A promotion or demotion changes the designation. Enter the new designation, or choose rate adjustment.';
  if (rule === 'keep' && !same)
    return 'A rate adjustment keeps the designation. Choose promotion or demotion to change it.';
  return null;
}
