import { Injectable, computed, inject, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { asApiError } from '../../core/api/api-error';
import { DEFAULT_PAGE_SIZE, PagedResult } from '../../core/api/paged-result';
import { injectListQuery } from '../../shared/list-query/list-query';
import { PayrollApi } from './payroll-api';
import { PayrollRunListItem } from './payroll.models';

/** The payroll list: the page comes from the address bar, the runs (newest first) from the API. Provide it on the list page. */
@Injectable()
export class PayrollListState {
  private readonly api = inject(PayrollApi);

  readonly query = injectListQuery({});

  private readonly resource = rxResource({
    params: () => this.query.values(),
    stream: ({ params }) => this.api.list({ page: params.page, pageSize: DEFAULT_PAGE_SIZE }),
  });

  /** The latest page of runs; the one before it while the next loads. */
  readonly result = linkedSignal<
    PagedResult<PayrollRunListItem> | undefined,
    PagedResult<PayrollRunListItem> | undefined
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
