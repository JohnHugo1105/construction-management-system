import { Injectable, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { asApiError } from '../../../core/api/api-error';
import { DEFAULT_PAGE_SIZE } from '../../../core/api/paged-result';
import { injectListQuery, listField } from '../../../shared/list-query/list-query';
import { ProjectContext } from '../../projects/project-context';
import { CostingApi } from '../costing-api';
import { CostEntry, CostEntryFilters, TotaledPage } from '../costing.models';
import { divisionField, idField, itemTypeField, originField } from '../list-fields';
import { latestValue } from '../resource-helpers';

/** What the Costs tab keeps in its address: the search, the filters and the page. */
export const COSTS_QUERY = {
  search: listField.text(),
  subProject: idField(),
  division: divisionField(),
  itemType: itemTypeField(),
  origin: originField(),
  from: listField.date(),
  to: listField.date(),
  voided: listField.flag(),
};

type CostsQueryValues = {
  readonly search: string;
  readonly subProject: string;
  readonly division: number;
  readonly itemType: string;
  readonly origin: string;
  readonly from: string;
  readonly to: string;
  readonly voided: boolean;
};

/** The address's words as the API's filters. */
export function toCostFilters(values: CostsQueryValues): CostEntryFilters {
  return {
    subProjectId: values.subProject,
    division: values.division,
    itemType: values.itemType,
    origin: values.origin,
    from: values.from,
    to: values.to,
    search: values.search,
    includeVoided: values.voided,
  };
}

/**
 * The Costs tab's data: the cost entries of the open project, filtered by what the address bar says, with the totals of the
 * whole filtered set. The previous rows stay on screen (dimmed) while the next ones load. Provide it on the tab.
 */
@Injectable()
export class CostsState {
  private readonly api = inject(CostingApi);
  private readonly context = inject(ProjectContext);

  readonly query = injectListQuery(COSTS_QUERY);
  readonly filters = computed(() => toCostFilters(this.query.values()));
  readonly pageSize = DEFAULT_PAGE_SIZE;

  /** Filters in use besides the search box (which is always on screen): how many, for the "Filters (2)" button. */
  readonly activeFilterCount = computed(() => {
    const filters = this.filters();
    return [
      filters.subProjectId !== '',
      filters.division > 0,
      filters.itemType !== '',
      filters.origin !== '',
      filters.from !== '',
      filters.to !== '',
      filters.includeVoided,
    ].filter(Boolean).length;
  });

  private readonly resource = rxResource({
    params: () => {
      const projectId = this.context.projectId();
      return projectId
        ? { projectId, filters: this.filters(), page: this.query.values().page }
        : undefined;
    },
    stream: ({ params }) =>
      this.api.listCostEntries(params.projectId, params.filters, {
        page: params.page,
        pageSize: DEFAULT_PAGE_SIZE,
      }),
  });

  readonly result = latestValue<TotaledPage<CostEntry>>(this.resource);
  readonly entries = computed(() => this.result()?.items ?? []);
  readonly total = computed(() => this.result()?.total ?? 0);
  readonly totals = computed(() => this.result()?.totals ?? null);
  readonly loadingFirst = computed(() => this.resource.isLoading() && this.result() === undefined);
  readonly refreshing = computed(() => this.resource.isLoading() && this.result() !== undefined);
  readonly error = computed(() => {
    const failure = this.resource.error();
    return failure ? asApiError(failure) : null;
  });

  reload(): void {
    this.resource.reload();
  }

  /** Back to every entry: the filters are cleared, the search the person typed is kept. */
  clearFilters(): void {
    this.query.set({
      subProject: '',
      division: 0,
      itemType: '',
      origin: '',
      from: '',
      to: '',
      voided: false,
    });
  }

  /**
   * Shows the entry that already holds a supplier invoice, with every other filter cleared so nothing hides it. The server
   * decides "same invoice" loosely (case, spaces and punctuation are ignored) while a text search is literal, so when the
   * server named the earlier entry's date the list is that day's entries from that supplier; otherwise it searches the invoice
   * number as typed.
   */
  showExisting(invoice: { supplier: string; invoiceNumber: string; date: string | null }): void {
    const byDate = invoice.date !== null && invoice.supplier !== '';
    this.query.set({
      search: byDate ? invoice.supplier : invoice.invoiceNumber,
      subProject: '',
      division: 0,
      itemType: '',
      origin: '',
      from: byDate ? (invoice.date ?? '') : '',
      to: byDate ? (invoice.date ?? '') : '',
      voided: false,
    });
  }
}
