import { Injectable, computed, inject, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { asApiError } from '../../core/api/api-error';
import { DEFAULT_PAGE_SIZE, PagedResult } from '../../core/api/paged-result';
import { injectListQuery, listField } from '../../shared/list-query/list-query';
import { SubcontractsApi } from './subcontracts-api';
import { SubcontractListItem, SubcontractStatus } from './subcontracts.models';

const STATUSES: readonly ('' | SubcontractStatus)[] = ['', 'Draft', 'Active', 'Completed', 'Cancelled'];

/**
 * The subcontracts list: the filters (project, status, search) and page live in the address bar, the rows come from the API.
 * Provide it on the page that shows the list.
 */
@Injectable()
export class SubcontractListState {
  private readonly api = inject(SubcontractsApi);

  readonly query = injectListQuery({
    project: listField.text(),
    status: listField.choice<'' | SubcontractStatus>(STATUSES, ''),
    search: listField.text(),
  });

  private readonly resource = rxResource({
    params: () => this.query.values(),
    stream: ({ params }) =>
      this.api.list({
        projectId: params.project,
        status: params.status,
        search: params.search,
        page: params.page,
        pageSize: DEFAULT_PAGE_SIZE,
      }),
  });

  /** The latest page; the one before it while the next loads. */
  readonly result = linkedSignal<
    PagedResult<SubcontractListItem> | undefined,
    PagedResult<SubcontractListItem> | undefined
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
