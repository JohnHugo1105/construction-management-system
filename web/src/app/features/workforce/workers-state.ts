import { Injectable, computed, inject, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { asApiError } from '../../core/api/api-error';
import { DEFAULT_PAGE_SIZE, PagedResult } from '../../core/api/paged-result';
import { injectListQuery, listField } from '../../shared/list-query/list-query';
import { WORKER_FILTERS } from './worker-status';
import { WorkforceApi } from './workforce-api';
import { WorkerListItem } from './workforce.models';

/**
 * The worker list: what to show comes from the address bar (search, status, project, page), the rows come from the API, and
 * the previous page stays on screen (dimmed) while the next one loads. Provide it on the list page.
 */
@Injectable()
export class WorkersState {
  private readonly api = inject(WorkforceApi);

  readonly query = injectListQuery({
    search: listField.text(),
    status: listField.choice(WORKER_FILTERS, 'All'),
    project: listField.text(),
  });

  private readonly resource = rxResource({
    params: () => this.query.values(),
    stream: ({ params }) =>
      this.api.list({
        search: params.search,
        status: params.status === 'All' ? '' : params.status,
        projectId: params.project,
        page: params.page,
        pageSize: DEFAULT_PAGE_SIZE,
      }),
  });

  /** The latest page of rows; the one before it while the next loads. */
  readonly result = linkedSignal<
    PagedResult<WorkerListItem> | undefined,
    PagedResult<WorkerListItem> | undefined
  >({
    source: () => (this.resource.hasValue() ? this.resource.value() : undefined),
    computation: (value, previous) => value ?? previous?.value,
  });

  readonly items = computed(() => this.result()?.items ?? []);
  readonly total = computed(() => this.result()?.total ?? 0);
  readonly pageSize = DEFAULT_PAGE_SIZE;

  /** True only before the first rows arrive. */
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
