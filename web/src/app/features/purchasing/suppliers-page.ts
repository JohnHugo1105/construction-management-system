import { ChangeDetectionStrategy, Component, Injectable, computed, inject, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { asApiError } from '../../core/api/api-error';
import { DEFAULT_PAGE_SIZE, PagedResult } from '../../core/api/paged-result';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { injectListQuery, listField } from '../../shared/list-query/list-query';
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
import { PurchasingApi } from './purchasing-api';
import { kindLabel } from './purchasing-presentation';
import { PurchasingTabs } from './purchasing-tabs';
import { SupplierListItem } from './purchasing.models';

export const KIND_FILTERS = ['All', 'Supplier', 'Subcontractor'] as const;

/** The firms list: search, kind and archived come from the address bar; the previous page stays (dimmed) while the next loads. */
@Injectable()
export class SupplierListState {
  private readonly api = inject(PurchasingApi);

  readonly query = injectListQuery({
    search: listField.text(),
    kind: listField.choice(KIND_FILTERS, 'All'),
    archived: listField.flag(),
  });

  private readonly resource = rxResource({
    params: () => this.query.values(),
    stream: ({ params }) =>
      this.api.listSuppliers({
        search: params.search,
        kind: params.kind === 'All' ? '' : params.kind,
        includeArchived: params.archived,
        page: params.page,
        pageSize: DEFAULT_PAGE_SIZE,
      }),
  });

  readonly result = linkedSignal<
    PagedResult<SupplierListItem> | undefined,
    PagedResult<SupplierListItem> | undefined
  >({
    source: () => (this.resource.hasValue() ? this.resource.value() : undefined),
    computation: (value, previous) => value ?? previous?.value,
  });
  readonly items = computed(() => this.result()?.items ?? []);
  readonly total = computed(() => this.result()?.total ?? 0);
  readonly pageSize = DEFAULT_PAGE_SIZE;
  readonly loadingFirst = computed(() => this.resource.isLoading() && this.result() === undefined);
  readonly refreshing = computed(() => this.resource.isLoading() && this.result() !== undefined);
  readonly error = computed(() => {
    const failure = this.resource.error();
    return failure ? asApiError(failure) : null;
  });

  reload(): void {
    this.resource.reload();
  }
}

/**
 * The firms the company buys from and hires: one spelling each, so orders and invoices name the same firm. People who manage firms
 * get the one next step, "Add a firm"; archived firms sit behind "Show archived firms".
 */
@Component({
  selector: 'app-suppliers-page',
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
  providers: [SupplierListState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './suppliers-page.html',
  styleUrl: './purchasing-page.scss',
})
export class SuppliersPage {
  protected readonly state = inject(SupplierListState);
  protected readonly viewport = inject(Viewport);
  private readonly session = inject(SessionState);

  protected readonly canManage = this.session.permit(Permission.suppliersManage);
  protected readonly kinds = KIND_FILTERS;

  /** Nothing to filter yet: only the one next step shows, not a search box and chips for a list that does not exist. */
  protected readonly bare = computed(
    () =>
      !this.state.loadingFirst() &&
      this.state.items().length === 0 &&
      !this.state.query.isFiltered() &&
      this.state.error() === null,
  );

  protected readonly rows = computed(() =>
    this.state.items().map((firm) => ({
      id: firm.id,
      name: firm.name,
      kind: kindLabel(firm),
      contact: [firm.contactPerson, firm.phone, firm.email].filter(Boolean).join(' · '),
      archived: firm.isArchived,
    })),
  );

  protected archivedChanged(event: Event): void {
    this.state.query.set({ archived: (event.target as HTMLInputElement).checked });
  }
}
