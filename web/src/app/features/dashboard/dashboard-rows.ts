import { IconName } from '../../shared/ui/icon/icon';
import { ChipTone } from '../../shared/ui/status-chip/status-chip';
import { isForecastOver } from '../costing/costing.models';
import { countAttention, describeAttention, healthOf, presentHealth } from '../costing/health';
import { DashboardRow } from './dashboard.models';

/** Over budget comes first, then the projects forecast to finish over budget; every other health follows in its usual order. */
const FORECAST_GROUP = 0.5;

function groupOf(row: DashboardRow): number {
  const health = healthOf(row.health);
  if (health === 'OverBudget') return presentHealth(health).severity;
  return isForecastOver(row.progressForecast) ? FORECAST_GROUP : presentHealth(health).severity + 1;
}

/**
 * The dashboard's rows, what needs attention first: over budget; then the projects not yet over budget that are forecast to finish
 * over it, the biggest overrun first; then unbudgeted, near limit, on track and no activity. Rows of a group go by name.
 */
export function sortDashboardRows(rows: readonly DashboardRow[]): DashboardRow[] {
  return [...rows].sort(
    (a, b) =>
      groupOf(a) - groupOf(b) ||
      (groupOf(a) === FORECAST_GROUP
        ? (b.progressForecast?.overBudgetMinor ?? 0) - (a.progressForecast?.overBudgetMinor ?? 0)
        : 0) ||
      a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }),
  );
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
 * limit. The API judges each project on its whole budget, so when none counts the headline says only what that proves ("no project is
 * over its total budget", never "everything is fine") and points to the project's own breakdown, where a single part can still be over.
 *
 * `forecastOver` is how many projects the work done says will finish over budget (the server's count). It never changes the
 * health above: it adds to the detail when something already needs attention, and on its own it replaces the calm "no project is
 * over" headline, so the headline never reads calmer than a project's Progress tab.
 */
export function leadFact(rows: readonly DashboardRow[], forecastOver = 0): LeadFact {
  const counts = countAttention(rows);
  if (counts.total > 0) {
    const detail = describeAttention(counts);
    return {
      headline:
        counts.total === 1
          ? '1 project needs attention'
          : `${counts.total} projects need attention`,
      detail:
        forecastOver > 0 ? `${detail}, ${forecastOver} forecast to finish over budget` : detail,
      tone: 'warning',
      icon: 'warning',
    };
  }
  if (forecastOver > 0) {
    return {
      headline:
        forecastOver === 1
          ? '1 project forecast to finish over budget'
          : `${forecastOver} projects forecast to finish over budget`,
      detail:
        "Spending is running ahead of the work done. Open the project's Progress tab to see why.",
      tone: 'warning',
      icon: 'warning',
    };
  }
  if (rows.some((row) => healthOf(row.health) === 'OnTrack')) {
    return {
      headline: 'No project is over its total budget',
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
