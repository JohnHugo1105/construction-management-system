import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { formatDate } from '../../shared/presentation/business-time';
import { formatMoney } from '../../shared/presentation/money';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { Icon } from '../../shared/ui/icon/icon';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Pager } from '../../shared/ui/pager/pager';
import { SearchBox } from '../../shared/ui/search-box/search-box';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { OrderListState } from './order-list-state';
import { PurchasingApi } from './purchasing-api';
import { STATUS_FILTERS, listDeliveryChip, presentStatus } from './purchasing-presentation';
import { PurchasingTabs } from './purchasing-tabs';

/**
 * The purchase orders list: search, status chips and a project filter, a table on wide screens and cards on phones, paged by the
 * server. People who may draft orders get the one next step, "New purchase order".
 */
@Component({
  selector: 'app-purchasing-page',
  imports: [
    RouterLink,
    PageHeader,
    PurchasingTabs,
    SearchBox,
    Button,
    Icon,
    StatusChip,
    Pager,
    EmptyState,
    Skeleton,
    ApiErrorAlert,
  ],
  providers: [OrderListState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './purchasing-page.html',
  styleUrl: './purchasing-page.scss',
})
export class PurchasingPage {
  protected readonly state = inject(OrderListState);
  protected readonly viewport = inject(Viewport);
  private readonly session = inject(SessionState);
  private readonly api = inject(PurchasingApi);

  protected readonly canManage = this.session.permit(Permission.purchasingManage);
  protected readonly filters = STATUS_FILTERS;
  protected readonly currency = this.session.currencyCode;

  private readonly projects = toSignal(
    this.api.projectLookup(true).pipe(catchError(() => of([]))),
    { initialValue: [] },
  );

  /** Every project, archived ones too: an order of a project that was archived afterwards is still listed. */
  protected readonly projectOptions = computed(() => this.projects());

  /** Nothing to filter yet: only the one next step shows, not a search box and chips for a list that does not exist. */
  protected readonly bare = computed(
    () =>
      !this.state.loadingFirst() &&
      this.state.items().length === 0 &&
      !this.state.query.isFiltered() &&
      this.state.error() === null,
  );

  protected readonly rows = computed(() =>
    this.state.items().map((order) => ({
      id: order.id,
      number: order.number,
      supplier: order.supplierName,
      project: order.projectName,
      status: presentStatus(order.status),
      delivery: listDeliveryChip(order),
      orderDate: formatDate(order.orderDate),
      total: formatMoney(order.grossMinor, this.currency()),
      lines: order.lineCount === 1 ? '1 line' : `${order.lineCount} lines`,
    })),
  );

  protected projectChanged(event: Event): void {
    this.state.query.set({ project: (event.target as HTMLSelectElement).value });
  }
}
