import { Injectable, computed, inject, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { asApiError } from '../../core/api/api-error';
import { DEFAULT_PAGE_SIZE } from '../../core/api/paged-result';
import { injectListQuery, listField } from '../../shared/list-query/list-query';
import { ReceivingApi } from './receiving-api';
import { ReceivingOrderPage } from './receiving.models';

/**
 * The orders awaiting delivery: what to show comes from the address bar (search, page), the rows come from the API, and the
 * previous page stays on screen (dimmed) while the next one loads. Provide it on the page.
 */
@Injectable()
export class ReceivingListState {
  private readonly api = inject(ReceivingApi);

  readonly query = injectListQuery({ search: listField.text() });

  private readonly resource = rxResource({
    params: () => this.query.values(),
    stream: ({ params }) =>
      this.api.listOrders({ search: params.search, page: params.page, pageSize: DEFAULT_PAGE_SIZE }),
  });

  readonly result = linkedSignal<ReceivingOrderPage | undefined, ReceivingOrderPage | undefined>({
    source: () => (this.resource.hasValue() ? this.resource.value() : undefined),
    computation: (value, previous) => value ?? previous?.value,
  });

  readonly items = computed(() => this.result()?.items ?? []);
  readonly orderCount = computed(() => this.result()?.orderCount ?? 0);
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
