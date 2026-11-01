import { Injectable, computed, inject, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { asApiError } from '../../core/api/api-error';
import { DEFAULT_PAGE_SIZE, PagedResult } from '../../core/api/paged-result';
import { ListQueryField, injectListQuery, listField } from '../../shared/list-query/list-query';
import { PurchasingApi } from './purchasing-api';
import { STATUS_FILTERS } from './purchasing-presentation';
import { PurchaseOrderListItem } from './purchasing.models';

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** An id in the address (the project filter). Anything that is not an id means "every project". */
export const idField: ListQueryField<string> = {
  defaultValue: '',
  parse: (raw) => (raw !== null && GUID.test(raw) ? raw : ''),
  serialize: (value) => (value === '' ? null : value),
};

/**
 * The orders list: what to show comes from the address bar (search, status, project, page), the rows come from the API, and the
 * previous page stays on screen (dimmed) while the next one loads. Provide it on the page.
 */
@Injectable()
export class OrderListState {
  private readonly api = inject(PurchasingApi);

  readonly query = injectListQuery({
    search: listField.text(),
    status: listField.choice(STATUS_FILTERS, 'All'),
    project: idField,
  });

  private readonly resource = rxResource({
    params: () => this.query.values(),
    stream: ({ params }) =>
      this.api.listOrders({
        search: params.search,
        status: params.status === 'All' ? '' : params.status,
        projectId: params.project,
        supplierId: '',
        page: params.page,
        pageSize: DEFAULT_PAGE_SIZE,
      }),
  });

  readonly result = linkedSignal<
    PagedResult<PurchaseOrderListItem> | undefined,
    PagedResult<PurchaseOrderListItem> | undefined
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
