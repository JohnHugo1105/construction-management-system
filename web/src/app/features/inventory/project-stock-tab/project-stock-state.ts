import { Injectable, computed, inject, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { asApiError } from '../../../core/api/api-error';
import { DEFAULT_PAGE_SIZE, PagedResult } from '../../../core/api/paged-result';
import { injectListQuery } from '../../../shared/list-query/list-query';
import { ProjectContext } from '../../projects/project-context';
import { InventoryApi } from '../inventory-api';
import { ProjectStock } from '../inventory.models';

/** What is on the open project now, paged by the server (`?page=2` in the address). Provide it on the Stock tab. */
@Injectable()
export class ProjectStockState {
  private readonly api = inject(InventoryApi);
  private readonly context = inject(ProjectContext);

  readonly query = injectListQuery({});
  readonly pageSize = DEFAULT_PAGE_SIZE;

  private readonly resource = rxResource({
    params: () => {
      const projectId = this.context.projectId();
      return projectId ? { projectId, page: this.query.values().page } : undefined;
    },
    stream: ({ params }) => this.api.projectStock(params.projectId, params.page, DEFAULT_PAGE_SIZE),
  });

  /** The latest page of rows; the one before it while the next loads. */
  readonly result = linkedSignal<
    PagedResult<ProjectStock> | undefined,
    PagedResult<ProjectStock> | undefined
  >({
    source: () => (this.resource.hasValue() ? this.resource.value() : undefined),
    computation: (value, previous) => value ?? previous?.value,
  });

  readonly items = computed(() => this.result()?.items ?? []);
  readonly total = computed(() => this.result()?.total ?? 0);
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
