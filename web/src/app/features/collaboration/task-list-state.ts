import { Injectable, computed, inject, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { asApiError } from '../../core/api/api-error';
import { DEFAULT_PAGE_SIZE, PagedResult } from '../../core/api/paged-result';
import { injectListQuery, listField } from '../../shared/list-query/list-query';
import { CollaborationApi } from './collaboration-api';
import { TaskListItem, TaskScope, TaskStatus } from './collaboration.models';

const SCOPES: readonly TaskScope[] = ['assigned', 'created', 'all'];
const STATUSES: readonly ('' | TaskStatus)[] = ['', 'Open', 'Done', 'Cancelled'];

/**
 * The Tasks list: whose tasks (assigned to me, created by me, or every task for an Administrator), the state and the project live in the
 * address bar; the rows come from the API, Open first with the overdue and soonest due on top. It opens on the Open tasks assigned to me.
 * Provide it on the page that shows the list.
 */
@Injectable()
export class TaskListState {
  private readonly api = inject(CollaborationApi);

  readonly query = injectListQuery({
    scope: listField.choice<TaskScope>(SCOPES, 'assigned'),
    status: listField.choice<'' | TaskStatus>(STATUSES, 'Open'),
    project: listField.text(),
  });

  private readonly resource = rxResource({
    params: () => this.query.values(),
    stream: ({ params }) =>
      this.api.listTasks({
        scope: params.scope,
        status: params.status,
        projectId: params.project,
        page: params.page,
        pageSize: DEFAULT_PAGE_SIZE,
      }),
  });

  /** The latest page; the one before it while the next loads. */
  readonly result = linkedSignal<
    PagedResult<TaskListItem> | undefined,
    PagedResult<TaskListItem> | undefined
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
