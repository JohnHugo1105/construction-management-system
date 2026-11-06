import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { formatMoney } from '../../shared/presentation/money';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { Icon } from '../../shared/ui/icon/icon';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { describeBasis, describeRemaining, describeUsed } from '../costing/figures';
import { presentHealth } from '../costing/health';
import { CommitmentLine } from '../costing/ui/commitment-line';
import { isForecastOver } from '../costing/costing.models';
import { CostPosition } from '../costing/ui/cost-position';
import { injectMinWidth } from '../costing/ui/container-width';
import { ScrollRegion } from '../costing/ui/scroll-region';
import { UsedBar } from '../costing/ui/used-bar';
import { PROJECT_STATUS } from '../projects/project-status';
import { DashboardState } from './dashboard-state';
import { ForecastChip } from './forecast-chip';
import { leadFact, sortDashboardRows } from './dashboard-rows';

/**
 * The dashboard: one fact first ("2 projects need attention"), the company's budget against spent cost in a quiet band, then a
 * row per project, worst first. Each row opens that project's Budget tab. A table on wide screens and cards on phones.
 */
@Component({
  selector: 'app-dashboard-page',
  imports: [
    RouterLink,
    PageHeader,
    ApiErrorAlert,
    Button,
    EmptyState,
    Icon,
    Skeleton,
    StatusChip,
    CostPosition,
    CommitmentLine,
    ForecastChip,
    ScrollRegion,
    UsedBar,
  ],
  providers: [DashboardState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.scss',
})
export class DashboardPage {
  protected readonly state = inject(DashboardState);
  protected readonly viewport = inject(Viewport);
  /** Seven columns need about this much room; with less the projects are cards, so nothing is hidden behind a sideways scroll. */
  protected readonly roomForTable = injectMinWidth(64);
  private readonly session = inject(SessionState);

  protected readonly canManageProjects = this.session.permit(Permission.projectsManage);

  protected readonly lead = computed(() => {
    const data = this.state.dashboard();
    return data ? leadFact(data.projects, data.totals.forecastOverBudgetCount ?? 0) : null;
  });

  protected readonly basisLine = computed(() => {
    const data = this.state.dashboard();
    return data ? describeBasis(data.basis) : '';
  });

  /** The company-wide band is shown once there is any budget or spending to add up. */
  protected readonly showBand = computed(() => {
    const data = this.state.dashboard();
    return data !== undefined && data.totals.health !== 'NoActivity';
  });

  /** Margins have a column only when at least one project has a contract price to work them out from. */
  protected readonly showMargin = computed(() =>
    (this.state.dashboard()?.projects ?? []).some((row) => row.marginAfterActualMinor !== null),
  );

  /** The rows worst first, with their words and amounts worked out so the template only prints them. */
  protected readonly rows = computed(() => {
    const data = this.state.dashboard();
    if (!data) return [];
    const currency = this.session.currencyCode();
    return sortDashboardRows(data.projects).map((row) => {
      const health = presentHealth(row.health);
      const idle = row.health === 'NoActivity';
      const margin = row.marginAfterActualMinor;
      return {
        row,
        link: ['/projects', row.id, 'budget'],
        status: PROJECT_STATUS[row.status].label,
        health,
        idle,
        /** How far over the budget the work done says the project will finish; null when it is not forecast over. */
        forecastOver: isForecastOver(row.progressForecast)
          ? (row.progressForecast?.overBudgetMinor ?? null)
          : null,
        budget: formatMoney(row.budgetMinor, currency),
        spent: formatMoney(row.actualMinor, currency),
        remaining: describeRemaining(row.remainingMinor, currency),
        used: describeUsed(row.usedBps),
        margin:
          margin === null ? null : { text: formatMoney(margin, currency), negative: margin < 0 },
      };
    });
  });
}
