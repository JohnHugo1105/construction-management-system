import { Injectable, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { asApiError } from '../../../core/api/api-error';
import { DEFAULT_PAGE_SIZE } from '../../../core/api/paged-result';
import { injectListQuery, listField } from '../../../shared/list-query/list-query';
import { ProjectContext } from '../../projects/project-context';
import { CostingApi } from '../costing-api';
import { BudgetLine, BudgetLineFilters, CostSummary, TotaledPage } from '../costing.models';
import { divisionField, idField, itemTypeField } from '../list-fields';
import { latestValue } from '../resource-helpers';

/** What the Budget tab keeps in its address: the search, the filters and the page. */
export const BUDGET_QUERY = {
  search: listField.text(),
  subProject: idField(),
  division: divisionField(),
  itemType: itemTypeField(),
  removed: listField.flag(),
};

type BudgetQueryValues = {
  readonly search: string;
  readonly subProject: string;
  readonly division: number;
  readonly itemType: string;
  readonly removed: boolean;
};

/** The address's words as the API's filters. */
export function toBudgetFilters(values: BudgetQueryValues): BudgetLineFilters {
  return {
    subProjectId: values.subProject,
    division: values.division,
    itemType: values.itemType,
    search: values.search,
    includeRemoved: values.removed,
  };
}

/**
 * The Budget tab's data: the project's position (cost summary) and its budget lines. What the lines show comes from the address
 * bar; the previous rows stay on screen (dimmed) while the next ones load. Provide it on the tab.
 */
@Injectable()
export class BudgetState {
  private readonly api = inject(CostingApi);
  private readonly context = inject(ProjectContext);

  readonly query = injectListQuery(BUDGET_QUERY);
  readonly filters = computed(() => toBudgetFilters(this.query.values()));
  readonly pageSize = DEFAULT_PAGE_SIZE;

  /** Filters in use besides the search box (which is always on screen): how many, for the "Filters (2)" button. */
  readonly activeFilterCount = computed(() => {
    const filters = this.filters();
    return [
      filters.subProjectId !== '',
      filters.division > 0,
      filters.itemType !== '',
      filters.includeRemoved,
    ].filter(Boolean).length;
  });

  private readonly linesResource = rxResource({
    params: () => {
      const projectId = this.context.projectId();
      return projectId
        ? { projectId, filters: this.filters(), page: this.query.values().page }
        : undefined;
    },
    stream: ({ params }) =>
      this.api.listBudgetLines(params.projectId, params.filters, {
        page: params.page,
        pageSize: DEFAULT_PAGE_SIZE,
      }),
  });

  private readonly summaryResource = rxResource({
    params: () => this.context.projectId() ?? undefined,
    stream: ({ params }) => this.api.costSummary(params),
  });

  readonly result = latestValue<TotaledPage<BudgetLine>>(this.linesResource);
  readonly lines = computed(() => this.result()?.items ?? []);
  readonly total = computed(() => this.result()?.total ?? 0);
  readonly totals = computed(() => this.result()?.totals ?? null);
  readonly loadingFirst = computed(
    () => this.linesResource.isLoading() && this.result() === undefined,
  );
  readonly refreshing = computed(
    () => this.linesResource.isLoading() && this.result() !== undefined,
  );
  readonly error = computed(() => {
    const failure = this.linesResource.error();
    return failure ? asApiError(failure) : null;
  });

  readonly summary = latestValue<CostSummary>(this.summaryResource);
  readonly summaryLoading = computed(
    () => this.summaryResource.isLoading() && this.summary() === undefined,
  );
  readonly summaryError = computed(() => {
    const failure = this.summaryResource.error();
    return failure ? asApiError(failure) : null;
  });

  /** Fetches the lines and the position again, after a line was added, changed or removed. */
  reload(): void {
    this.linesResource.reload();
    this.summaryResource.reload();
  }

  reloadLines(): void {
    this.linesResource.reload();
  }

  reloadSummary(): void {
    this.summaryResource.reload();
  }

  /** Back to every line: the filters are cleared, the search the person typed is kept. */
  clearFilters(): void {
    this.query.set({ subProject: '', division: 0, itemType: '', removed: false });
  }
}
