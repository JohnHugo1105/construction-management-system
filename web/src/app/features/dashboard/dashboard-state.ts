import { Injectable, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { asApiError } from '../../core/api/api-error';
import { injectListQuery, listField } from '../../shared/list-query/list-query';
import { latestValue } from '../costing/resource-helpers';
import { DashboardApi } from './dashboard-api';
import { Dashboard } from './dashboard.models';

/**
 * The dashboard's data. Whether archived projects are included lives in the address bar (`?archived=1`), so a refresh or a
 * shared link shows the same list. The previous figures stay on screen (dimmed) while the next ones load.
 */
@Injectable()
export class DashboardState {
  private readonly api = inject(DashboardApi);

  readonly query = injectListQuery({ archived: listField.flag() });
  readonly includeArchived = computed(() => this.query.values().archived);

  private readonly resource = rxResource({
    params: () => this.includeArchived(),
    stream: ({ params }) => this.api.load(params),
  });

  readonly dashboard = latestValue<Dashboard>(this.resource);
  readonly loadingFirst = computed(
    () => this.resource.isLoading() && this.dashboard() === undefined,
  );
  readonly refreshing = computed(() => this.resource.isLoading() && this.dashboard() !== undefined);
  readonly error = computed(() => {
    const failure = this.resource.error();
    return failure ? asApiError(failure) : null;
  });

  reload(): void {
    this.resource.reload();
  }

  setArchived(include: boolean): void {
    this.query.set({ archived: include });
  }
}
