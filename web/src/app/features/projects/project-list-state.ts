import { Injectable, computed, inject, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { asApiError } from '../../core/api/api-error';
import { DEFAULT_PAGE_SIZE, PagedResult } from '../../core/api/paged-result';
import { injectListQuery, listField } from '../../shared/list-query/list-query';
import { PROJECT_FILTERS } from './project-status';
import { ProjectsApi } from './projects-api';
import { ProjectListItem } from './projects.models';

/**
 * The project list: what to show comes from the address bar (search, status, page), the rows come from the API, and the
 * previous page stays on screen (dimmed) while the next one loads. Provide it on the list page.
 */
@Injectable()
export class ProjectListState {
  private readonly api = inject(ProjectsApi);

  readonly query = injectListQuery({
    search: listField.text(),
    status: listField.choice(PROJECT_FILTERS, 'All'),
  });

  private readonly resource = rxResource({
    params: () => this.query.values(),
    stream: ({ params }) =>
      this.api.list({
        search: params.search,
        status: params.status === 'All' ? '' : params.status,
        page: params.page,
        pageSize: DEFAULT_PAGE_SIZE,
      }),
  });

  /** The latest page of rows; the one before it while the next loads. */
  readonly result = linkedSignal<
    PagedResult<ProjectListItem> | undefined,
    PagedResult<ProjectListItem> | undefined
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
