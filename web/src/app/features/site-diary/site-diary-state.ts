import { Injectable, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { asApiError } from '../../core/api/api-error';
import { DEFAULT_PAGE_SIZE, PagedResult } from '../../core/api/paged-result';
import { SessionState } from '../../core/session/session-state';
import { injectListQuery } from '../../shared/list-query/list-query';
import { todayIn } from '../../shared/presentation/business-time';
import { latestValue } from '../costing/resource-helpers';
import { ProjectContext } from '../projects/project-context';
import { SiteDiaryApi } from './site-diary-api';
import { DiaryListItem } from './site-diary.models';

/**
 * The project's reports for the Diary tab: the page lives in the address bar, the rows come from the API, newest first. Provide it on
 * the tab. It keeps the last page on screen while the next one loads.
 */
@Injectable()
export class SiteDiaryListState {
  private readonly api = inject(SiteDiaryApi);
  private readonly context = inject(ProjectContext);
  private readonly session = inject(SessionState);

  readonly query = injectListQuery({});

  private readonly resource = rxResource({
    params: () => {
      const projectId = this.context.projectId();
      return projectId ? { projectId, page: this.query.values().page } : undefined;
    },
    stream: ({ params }) => this.api.list(params.projectId, params.page, DEFAULT_PAGE_SIZE),
  });

  readonly result = latestValue<PagedResult<DiaryListItem>>(this.resource);
  readonly items = computed(() => this.result()?.items ?? []);
  readonly total = computed(() => this.result()?.total ?? 0);
  readonly pageSize = DEFAULT_PAGE_SIZE;
  readonly page = computed(() => this.query.values().page);
  readonly loadingFirst = computed(() => this.resource.isLoading() && this.result() === undefined);
  readonly refreshing = computed(() => this.resource.isLoading() && this.result() !== undefined);
  readonly error = computed(() => {
    const failure = this.resource.error();
    return failure ? asApiError(failure) : null;
  });

  /** Today where the company works. */
  readonly today = computed(() => todayIn(this.session.timeZoneId()));

  /** Today's report is in the list. Only the first page can say so (the newest reports are on it). */
  readonly hasToday = computed(() => this.items().some((item) => item.reportDate === this.today()));

  /** The list is loaded and holds no report at all. */
  readonly isEmpty = computed(() => this.result() !== undefined && this.total() === 0);

  reload(): void {
    this.resource.reload();
  }
}
