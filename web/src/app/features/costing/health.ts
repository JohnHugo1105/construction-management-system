import { ChipTone, BUDGET_HEALTH } from '../../shared/ui/status-chip/status-chip';
import { IconName } from '../../shared/ui/icon/icon';
import { Health } from './costing.models';

/** How a health looks and how urgent it is, in one place: the dashboard, the budget breakdown and every chip agree. */
export interface HealthPresentation {
  readonly label: string;
  readonly tone: ChipTone;
  readonly icon: IconName;
  /** 0 is the worst: lists put the smallest number first. */
  readonly severity: number;
  /** One short sentence for places that explain the state. */
  readonly meaning: string;
}

export const HEALTH_PRESENTATION: Record<Health, HealthPresentation> = {
  OverBudget: {
    label: 'Over budget',
    tone: BUDGET_HEALTH['over-budget'].tone,
    icon: BUDGET_HEALTH['over-budget'].icon,
    severity: 0,
    meaning: 'More has been spent than was budgeted.',
  },
  Unbudgeted: {
    label: 'Unbudgeted',
    tone: BUDGET_HEALTH.unbudgeted.tone,
    icon: BUDGET_HEALTH.unbudgeted.icon,
    severity: 1,
    meaning: 'Money was spent where no budget was set.',
  },
  NearLimit: {
    label: 'Near limit',
    tone: BUDGET_HEALTH['near-limit'].tone,
    icon: BUDGET_HEALTH['near-limit'].icon,
    severity: 2,
    meaning: '90% or more of the budget is used.',
  },
  OnTrack: {
    label: 'On track',
    tone: BUDGET_HEALTH['on-track'].tone,
    icon: BUDGET_HEALTH['on-track'].icon,
    severity: 3,
    meaning: 'Less than 90% of the budget is used.',
  },
  NoActivity: {
    label: 'No activity',
    tone: 'neutral',
    icon: 'clock',
    severity: 4,
    meaning: 'Nothing is budgeted or spent yet.',
  },
};

/** The API's word for a health, or "No activity" for a word this client does not know (so a new one never breaks a page). */
export function healthOf(value: string | null | undefined): Health {
  return value !== null && value !== undefined && Object.hasOwn(HEALTH_PRESENTATION, value)
    ? (value as Health)
    : 'NoActivity';
}

export function presentHealth(value: string | null | undefined): HealthPresentation {
  return HEALTH_PRESENTATION[healthOf(value)];
}

/** Rows ordered worst first (over budget, unbudgeted, near limit, on track, no activity), then by name. */
export function sortBySeverity<T extends { readonly health: string; readonly name: string }>(
  rows: readonly T[],
): T[] {
  return [...rows].sort(
    (a, b) =>
      presentHealth(a.health).severity - presentHealth(b.health).severity ||
      a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }),
  );
}

/** How many of some rows are in each state that deserves a look. Rows on track or without activity are not counted. */
export interface AttentionCounts {
  readonly overBudget: number;
  readonly unbudgeted: number;
  readonly nearLimit: number;
  readonly total: number;
}

export function countAttention(rows: readonly { readonly health: string }[]): AttentionCounts {
  let overBudget = 0;
  let unbudgeted = 0;
  let nearLimit = 0;
  for (const row of rows) {
    const health = healthOf(row.health);
    if (health === 'OverBudget') overBudget++;
    else if (health === 'Unbudgeted') unbudgeted++;
    else if (health === 'NearLimit') nearLimit++;
  }
  return { overBudget, unbudgeted, nearLimit, total: overBudget + unbudgeted + nearLimit };
}

/** "1 over budget, 2 near the limit": only the states that are present, worst first. Empty when nothing needs a look. */
export function describeAttention(counts: AttentionCounts): string {
  const parts: string[] = [];
  if (counts.overBudget > 0) parts.push(`${counts.overBudget} over budget`);
  if (counts.unbudgeted > 0) parts.push(`${counts.unbudgeted} unbudgeted`);
  if (counts.nearLimit > 0) parts.push(`${counts.nearLimit} near the limit`);
  return parts.join(', ');
}
