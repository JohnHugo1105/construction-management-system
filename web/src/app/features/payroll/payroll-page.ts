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
import { Pager } from '../../shared/ui/pager/pager';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { PayrollListState } from './payroll-list-state';
import { PAYROLL_STATUS } from './payroll-rules';
import { runLabel } from './premium-rules';

/**
 * The payroll runs, newest first: a table on wide screens and cards on phones, paged by the server. Each shows its period (or the
 * year of a 13th-month payroll), its state and the API's totals. Only people who prepare payroll are offered "New payroll run", the
 * primary action, and "13th-month pay" beside it once there are payrolls to work it out from. The pay rules, the holiday calendar and
 * the contribution tables are quiet links at the foot.
 */
@Component({
  selector: 'app-payroll-page',
  imports: [
    RouterLink,
    PageHeader,
    Button,
    Icon,
    StatusChip,
    Pager,
    EmptyState,
    Skeleton,
    ApiErrorAlert,
  ],
  providers: [PayrollListState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './payroll-page.html',
  styleUrl: './payroll-page.scss',
})
export class PayrollPage {
  protected readonly state = inject(PayrollListState);
  protected readonly viewport = inject(Viewport);
  private readonly session = inject(SessionState);

  protected readonly canRun = this.session.permit(Permission.payrollRun);

  /** No runs yet: the empty state carries the one next step, so the toolbar button would only repeat it. */
  protected readonly empty = computed(
    () => !this.state.loadingFirst() && !this.state.error() && this.state.items().length === 0,
  );

  protected readonly rows = computed(() => {
    const currency = this.session.currencyCode();
    return this.state.items().map((run) => ({
      id: run.id,
      period: runLabel(run),
      status: PAYROLL_STATUS[run.status],
      workers: run.workerCount === 1 ? '1 worker' : `${run.workerCount} workers`,
      gross: formatMoney(run.totalGrossMinor, currency),
      deductions: formatMoney(run.totalDeductionsMinor, currency),
      net: formatMoney(run.totalNetMinor, currency),
    }));
  });
}
