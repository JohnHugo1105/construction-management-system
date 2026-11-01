import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom, map } from 'rxjs';
import { ApiError, asApiError } from '../../../core/api/api-error';
import { PagedResult } from '../../../core/api/paged-result';
import { ToastService } from '../../../core/notifications/toast-service';
import { Permission } from '../../../core/session/permissions';
import { SessionState } from '../../../core/session/session-state';
import { formatDateTime } from '../../../shared/presentation/business-time';
import { formatMoney } from '../../../shared/presentation/money';
import { Alert } from '../../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../../shared/ui/button/button';
import { ConfirmDialog } from '../../../shared/ui/dialog/confirm-dialog';
import { EmptyState } from '../../../shared/ui/empty-state/empty-state';
import { Icon } from '../../../shared/ui/icon/icon';
import { Pager } from '../../../shared/ui/pager/pager';
import { Skeleton } from '../../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../../shared/ui/status-chip/status-chip';
import { ReasonDialog } from '../../costing/ui/reason-dialog';
import { ProjectContext } from '../../projects/project-context';
import { ChangeOrderFormDialog } from '../change-order-form-dialog';
import { ChangeOrderPage } from '../change-order-page';
import { baselineView, effectParts, statusLook } from '../change-order-presentation';
import { ChangeOrdersApi } from '../change-orders-api';
import { BudgetBaseline, ChangeOrder, ChangeOrderListItem } from '../change-orders.models';

const PAGE_SIZE = 25;

/**
 * A project's Changes tab: the budget baseline (original, approved changes, revised, for the budget and the contract price) and the
 * change orders. Before the budget is locked it explains what locking does and offers it to approvers; afterwards the one next step
 * is "New change order". A change order opens on the same tab (`?order=<id>`), so its link can be shared.
 */
@Component({
  selector: 'app-project-changes-tab',
  imports: [
    RouterLink,
    Alert,
    ApiErrorAlert,
    Button,
    ConfirmDialog,
    EmptyState,
    Icon,
    Pager,
    Skeleton,
    StatusChip,
    ReasonDialog,
    ChangeOrderFormDialog,
    ChangeOrderPage,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './project-changes-tab.html',
  styleUrl: './project-changes-tab.scss',
})
export class ProjectChangesTab {
  protected readonly context = inject(ProjectContext);
  private readonly api = inject(ChangeOrdersApi);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  private readonly query = toSignal(
    this.route.queryParamMap.pipe(
      map((params) => ({
        order: params.get('order'),
        page: Math.max(1, Number(params.get('page')) || 1),
      })),
    ),
    { initialValue: { order: null, page: 1 } },
  );
  protected readonly openOrderId = computed(() => this.query().order);
  protected readonly page = computed(() => this.query().page);
  protected readonly pageSize = PAGE_SIZE;

  protected readonly baseline = signal<BudgetBaseline | null>(null);
  protected readonly baselineError = signal<ApiError | null>(null);
  protected readonly orders = signal<PagedResult<ChangeOrderListItem> | null>(null);
  protected readonly ordersError = signal<ApiError | null>(null);
  protected readonly actionError = signal<ApiError | null>(null);
  protected readonly creating = signal(false);
  protected readonly confirmingLock = signal(false);
  protected readonly locking = signal(false);
  protected readonly unlocking = signal(false);

  private readonly mayManage = this.session.permit(Permission.changeOrdersManage);
  private readonly mayApprove = this.session.permit(Permission.changeOrdersApprove);
  protected readonly canApprove = computed(() => this.mayApprove() && !this.context.isArchived());

  protected readonly card = computed(() => {
    const baseline = this.baseline();
    if (!baseline) return null;
    const currency = this.session.currencyCode();
    if (!baseline.locked)
      return {
        locked: false as const,
        budget: formatMoney(baseline.currentBudgetGrossMinor, currency),
        lines: baseline.activeLineCount,
      };
    return {
      locked: true as const,
      view: baselineView(baseline, currency),
      since: `Locked on ${formatDateTime(baseline.lockedAtUtc, this.session.timeZoneId())}${baseline.lockedBy ? ` by ${baseline.lockedBy}` : ''}.`,
      canUnlock: this.canApprove() && baseline.approvedCount === 0,
    };
  });

  /** "New change order" is the one next step once the budget is locked, for people who draft. */
  protected readonly canCreate = computed(
    () => this.mayManage() && !this.context.isArchived() && this.baseline()?.locked === true,
  );

  protected readonly rows = computed(() => {
    const currency = this.session.currencyCode();
    const zone = this.session.timeZoneId();
    return (this.orders()?.items ?? []).map((item) => ({
      item,
      status: statusLook(item.status, item.voidedFromStatus),
      label: item.number ?? 'Draft',
      effect: effectParts(item.budgetEffectGrossMinor, item.contractPriceDeltaMinor, currency),
      when: formatDateTime(item.decidedAtUtc ?? item.submittedAtUtc ?? item.createdAtUtc, zone),
    }));
  });

  protected readonly lockMessage = computed(() => {
    const baseline = this.baseline();
    if (!baseline) return '';
    const currency = this.session.currencyCode();
    const price = baseline.currentContractPriceMinor;
    return (
      `The budget of ${formatMoney(baseline.currentBudgetGrossMinor, currency)} with VAT (${baseline.activeLineCount} line${baseline.activeLineCount === 1 ? '' : 's'})` +
      (price === null ? '' : ` and the contract price of ${formatMoney(price, currency)}`) +
      ' become the original. After this, budget lines and the price change only through approved change orders.'
    );
  });

  constructor() {
    void this.loadBaseline();
    effect(() => {
      const page = this.page();
      if (this.openOrderId() === null) untracked(() => void this.loadOrders(page));
    });
  }

  protected async loadBaseline(): Promise<void> {
    this.baselineError.set(null);
    try {
      this.baseline.set(await firstValueFrom(this.api.baseline(this.context.project().id)));
    } catch (failure) {
      this.baselineError.set(asApiError(failure));
    }
  }

  protected async loadOrders(page = this.page()): Promise<void> {
    this.ordersError.set(null);
    try {
      this.orders.set(
        await firstValueFrom(this.api.list(this.context.project().id, page, PAGE_SIZE)),
      );
    } catch (failure) {
      this.ordersError.set(asApiError(failure));
    }
  }

  protected setPage(page: number): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page },
      queryParamsHandling: 'merge',
    });
  }

  protected open(id: string): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { order: id },
      queryParamsHandling: 'merge',
    });
  }

  protected created(order: ChangeOrder): void {
    this.creating.set(false);
    this.toast.success('Change order started. Add its lines, then submit it for approval.');
    this.open(order.id);
  }

  /** A decision changed the budget or the price: refresh the figures here and the project (its price and version). */
  protected changed(): void {
    void this.loadBaseline();
    void this.context.reload();
  }

  protected async lock(): Promise<void> {
    const baseline = this.baseline();
    if (!baseline || this.locking()) return;
    this.locking.set(true);
    this.actionError.set(null);
    try {
      this.baseline.set(
        await firstValueFrom(this.api.lock(baseline.projectId, baseline.projectVersion)),
      );
      this.toast.success('Budget locked. Changes now go through change orders.');
      void this.context.reload();
      void this.loadOrders();
    } catch (failure) {
      this.actionError.set(asApiError(failure));
    } finally {
      this.locking.set(false);
      this.confirmingLock.set(false);
    }
  }

  protected readonly performUnlock = (reason: string) => {
    const baseline = this.baseline();
    if (!baseline) throw new Error('The baseline has not loaded.');
    return this.api.unlock(baseline.projectId, reason, baseline.projectVersion);
  };

  protected async unlocked(): Promise<void> {
    this.unlocking.set(false);
    this.toast.success('Budget unlocked. Budget lines can be changed directly again.');
    await this.loadBaseline();
    void this.context.reload();
  }

  protected async unlockOutdated(): Promise<void> {
    this.unlocking.set(false);
    await this.loadBaseline();
  }
}
