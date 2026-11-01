import { ChangeDetectionStrategy, Component, computed, inject, linkedSignal, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { asApiError } from '../../../core/api/api-error';
import { DEFAULT_PAGE_SIZE, PagedResult } from '../../../core/api/paged-result';
import { Permission } from '../../../core/session/permissions';
import { SessionState } from '../../../core/session/session-state';
import { formatDate } from '../../../shared/presentation/business-time';
import { formatMoney } from '../../../shared/presentation/money';
import { ApiErrorAlert } from '../../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../../shared/ui/button/button';
import { EmptyState } from '../../../shared/ui/empty-state/empty-state';
import { Icon } from '../../../shared/ui/icon/icon';
import { Pager } from '../../../shared/ui/pager/pager';
import { Skeleton } from '../../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../../shared/ui/status-chip/status-chip';
import { ProjectContext } from '../../projects/project-context';
import { PurchasingApi } from '../purchasing-api';
import { presentStatus } from '../purchasing-presentation';
import { PurchaseOrderListItem } from '../purchasing.models';

/**
 * The Purchases tab of a project: its purchase orders, newest first, each linking to the order. People who may draft orders get
 * "New purchase order" with this project already chosen; an archived project is read-only and says so.
 */
@Component({
  selector: 'app-project-purchases-tab',
  imports: [RouterLink, ApiErrorAlert, Button, EmptyState, Icon, Pager, Skeleton, StatusChip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="stack">
      @if (canDraft()) {
        <p>
          <a appButton variant="primary" routerLink="/purchasing/orders/new" [queryParams]="{ project: projectId() }">
            <app-icon name="plus" [size]="18" />
            New purchase order
          </a>
        </p>
      }

      @if (error(); as problem) {
        <app-api-error-alert [error]="problem" [allowRetry]="true" reloadLabel="Try again" (reload)="resource.reload()" />
      }

      @if (loadingFirst()) {
        <app-skeleton variant="card" [lines]="3" label="Loading purchase orders" />
      } @else if (rows().length > 0) {
        <ul class="card-list" [attr.aria-busy]="resource.isLoading()">
          @for (row of rows(); track row.id) {
            <li class="row-card">
              <div class="row-card__head">
                <a class="row-card__title row-link row-link--stretch" [routerLink]="['/purchasing/orders', row.id]">{{ row.number }} · {{ row.supplier }}</a>
                <app-status-chip [label]="row.status.label" [tone]="row.status.tone" [icon]="row.status.icon" />
              </div>
              <p class="text-muted text-small">{{ row.date }}</p>
              <p class="money">{{ row.total }}</p>
            </li>
          }
        </ul>
        <app-pager [page]="page()" [pageSize]="pageSize" [total]="total()" noun="orders" (pageChange)="page.set($event)" />
      } @else if (!error()) {
        <app-empty-state
          icon="cart"
          title="No purchase orders for this project"
          [message]="emptyMessage()"
        />
      }
    </div>
  `,
})
export class ProjectPurchasesTab {
  private readonly context = inject(ProjectContext);
  private readonly session = inject(SessionState);
  private readonly api = inject(PurchasingApi);

  protected readonly pageSize = DEFAULT_PAGE_SIZE;
  protected readonly page = signal(1);
  protected readonly projectId = this.context.projectId;
  private readonly currency = this.session.currencyCode;

  protected readonly canDraft = computed(
    () => this.session.can(Permission.purchasingManage) && !this.context.isArchived(),
  );

  protected readonly resource = rxResource({
    params: () => {
      const projectId = this.context.projectId();
      return projectId ? { projectId, page: this.page() } : undefined;
    },
    stream: ({ params }) =>
      this.api.listOrders({ projectId: params.projectId, supplierId: '', status: '', search: '', page: params.page, pageSize: DEFAULT_PAGE_SIZE }),
  });

  private readonly result = linkedSignal<PagedResult<PurchaseOrderListItem> | undefined, PagedResult<PurchaseOrderListItem> | undefined>({
    source: () => (this.resource.hasValue() ? this.resource.value() : undefined),
    computation: (value, previous) => value ?? previous?.value,
  });

  protected readonly total = computed(() => this.result()?.total ?? 0);
  protected readonly loadingFirst = computed(() => this.resource.isLoading() && this.result() === undefined);
  protected readonly error = computed(() => {
    const failure = this.resource.error();
    return failure ? asApiError(failure) : null;
  });
  protected readonly rows = computed(() =>
    (this.result()?.items ?? []).map((order) => ({
      id: order.id,
      number: order.number,
      supplier: order.supplierName,
      status: presentStatus(order.status),
      date: formatDate(order.orderDate),
      total: formatMoney(order.grossMinor, this.currency()),
    })),
  );
  protected readonly emptyMessage = computed(() => {
    if (this.context.isArchived()) return 'This project is archived, so no new orders can be raised for it.';
    return this.canDraft()
      ? 'Raise one to a supplier. Once it is approved, what has not been invoiced counts in the forecast.'
      : 'Orders raised for this project appear here.';
  });
}
