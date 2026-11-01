import { IconName } from '../../shared/ui/icon/icon';
import { ChipTone } from '../../shared/ui/status-chip/status-chip';
import { countAttention, describeAttention, healthOf, sortBySeverity } from '../costing/health';
import { DashboardRow } from './dashboard.models';

/** The dashboard's rows, worst first (over budget, unbudgeted, near limit, on track, no activity), then by name. */
export function sortDashboardRows(rows: readonly DashboardRow[]): DashboardRow[] {
  return sortBySeverity(rows);
}

/** The one thing the dashboard leads with. */
export interface LeadFact {
  readonly headline: string;
  readonly detail: string;
  readonly tone: Extract<ChipTone, 'warning' | 'success' | 'neutral'>;
  readonly icon: IconName;
}

/**
 * "2 projects need attention", worked out from the rows' health: a project counts when it is over budget, unbudgeted or near its
 * limit. The API judges each project on its whole budget, so when none counts the headline says "within budget" (not "nothing
 * needs a look") and points to the project's own breakdown, where a single part can still be over.
 */
export function leadFact(rows: readonly DashboardRow[]): LeadFact {
  const counts = countAttention(rows);
  if (counts.total > 0) {
    return {
      headline:
        counts.total === 1
          ? '1 project needs attention'
          : `${counts.total} projects need attention`,
      detail: describeAttention(counts),
      tone: 'warning',
      icon: 'warning',
    };
  }
  if (rows.some((row) => healthOf(row.health) === 'OnTrack')) {
    return {
      headline: 'Every project is within budget',
      detail:
        "This looks at each project's whole budget. Open one to see which parts are close to their limit.",
      tone: 'success',
      icon: 'check-circle',
    };
  }
  return {
    headline: 'Nothing to track yet',
    detail: 'Spending shows here once a project has a budget or a cost.',
    tone: 'neutral',
    icon: 'clock',
  };
}
