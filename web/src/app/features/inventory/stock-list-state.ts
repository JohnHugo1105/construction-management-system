import { Injectable, computed, inject, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { asApiError } from '../../core/api/api-error';
import { DEFAULT_PAGE_SIZE, PagedResult } from '../../core/api/paged-result';
import { ListQueryField, injectListQuery, listField } from '../../shared/list-query/list-query';
import { InventoryApi } from './inventory-api';
import { CATEGORY_FILTERS } from './inventory-presentation';
import { InventoryListItem } from './inventory.models';
import { PlaceOption, StockPlaces, WAREHOUSE_KEY, isProjectKey } from './stock-places';

/** The place filter in the address: "warehouse" or a project's id. Anything else means "anywhere". */
export const placeField: ListQueryField<string> = {
  defaultValue: '',
  parse: (raw) => (raw !== null && (raw === WAREHOUSE_KEY || isProjectKey(raw)) ? raw : ''),
  serialize: (value) => (value === '' ? null : value),
};

/**
 * The stock list: what to show comes from the address bar (search, category, place, retired, page), the rows come from the API,
 * and the previous page stays on screen (dimmed) while the next one loads. Provide it on the list page with `StockPlaces`.
 */
@Injectable()
export class StockListState {
  private readonly api = inject(InventoryApi);
  private readonly places = inject(StockPlaces);

  readonly query = injectListQuery({
    search: listField.text(),
    category: listField.choice(CATEGORY_FILTERS, 'All'),
    place: placeField,
    retired: listField.flag(),
  });

  private readonly resource = rxResource({
    params: () => this.query.values(),
    stream: ({ params }) =>
      this.api.list({
        search: params.search,
        category: params.category === 'All' ? '' : params.category,
        status: params.retired ? 'Retired' : 'Active',
        projectId: isProjectKey(params.place) ? params.place : '',
        inWarehouse: params.place === WAREHOUSE_KEY,
        page: params.page,
        pageSize: DEFAULT_PAGE_SIZE,
      }),
  });

  /** The latest page of rows; the one before it while the next loads. */
  readonly result = linkedSignal<
    PagedResult<InventoryListItem> | undefined,
    PagedResult<InventoryListItem> | undefined
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

  /** What the place filter offers: anywhere, the warehouse, then each project (archived ones too, stock can still sit there). */
  readonly placeOptions = computed<readonly PlaceOption[]>(() => {
    const options: PlaceOption[] = [
      { value: '', label: 'Anywhere' },
      { value: WAREHOUSE_KEY, label: 'In the warehouse' },
      ...this.places.everyProject().map((place) => ({
        value: place.value,
        label: this.places.isArchived(place.value) ? `${place.label} (archived)` : place.label,
      })),
    ];
    const chosen = this.query.values().place;
    // A shared link can name a project that is no longer listed: keep it visible rather than showing "Anywhere".
    if (chosen !== '' && this.places.loaded() && !options.some((option) => option.value === chosen))
      options.push({ value: chosen, label: 'A project that is no longer listed' });
    return options;
  });

  reload(): void {
    this.resource.reload();
  }
}
