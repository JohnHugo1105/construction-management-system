import { Injectable, computed, inject, linkedSignal, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { asApiError } from '../../core/api/api-error';
import { DEFAULT_PAGE_SIZE, PagedResult } from '../../core/api/paged-result';
import { injectListQuery } from '../../shared/list-query/list-query';
import { InventoryApi } from './inventory-api';
import { InventoryItem, StockMovement } from './inventory.models';

/**
 * One item and its history. The item and the history page the person is on are separate requests, but they only ever change
 * together: after an action the server's answer replaces the item and the history goes back to its first page and is fetched
 * again, so balances, history and totals can never disagree. The history page is in the address (`?page=2`).
 */
@Injectable()
export class ItemDetailState {
  private readonly api = inject(InventoryApi);

  readonly id = signal<string | null>(null);
  /** Only the history page: the item has no filters. */
  readonly query = injectListQuery({});
  readonly pageSize = DEFAULT_PAGE_SIZE;

  private readonly itemResource = rxResource({
    params: () => this.id() ?? undefined,
    stream: ({ params }) => this.api.get(params),
  });

  private readonly historyResource = rxResource({
    params: () => {
      const id = this.id();
      return id ? { id, page: this.query.values().page } : undefined;
    },
    stream: ({ params }) => this.api.movements(params.id, params.page, DEFAULT_PAGE_SIZE),
  });

  /** The item as last loaded or answered by the server; null until it first arrives. */
  readonly item = computed<InventoryItem | null>(() =>
    this.itemResource.hasValue() ? this.itemResource.value() : null,
  );
  readonly itemLoading = computed(() => this.itemResource.isLoading() && this.item() === null);
  readonly itemError = computed(() => {
    const failure = this.itemResource.error();
    return failure && this.item() === null ? asApiError(failure) : null;
  });

  /** The latest page of history; the one before it while the next loads. */
  readonly history = linkedSignal<
    PagedResult<StockMovement> | undefined,
    PagedResult<StockMovement> | undefined
  >({
    source: () => (this.historyResource.hasValue() ? this.historyResource.value() : undefined),
    computation: (value, previous) => value ?? previous?.value,
  });
  readonly movements = computed(() => this.history()?.items ?? []);
  readonly total = computed(() => this.history()?.total ?? 0);
  readonly historyLoading = computed(
    () => this.historyResource.isLoading() && this.history() === undefined,
  );
  readonly historyRefreshing = computed(
    () => this.historyResource.isLoading() && this.history() !== undefined,
  );
  readonly historyError = computed(() => {
    const failure = this.historyResource.error();
    return failure ? asApiError(failure) : null;
  });

  /**
   * The server answered an action with the item as it now stands. Fetch the history's first page, then take the item and that
   * page together, so for no moment do the balances show the new state and the history the old one. If the history cannot be
   * fetched, the item is shown anyway and the history says for itself that it could not load.
   */
  async applied(item: InventoryItem): Promise<void> {
    const id = this.id();
    if (id && this.query.values().page === 1) {
      try {
        const newest = await firstValueFrom(this.api.movements(id, 1, DEFAULT_PAGE_SIZE));
        this.itemResource.set(item);
        this.historyResource.set(newest);
        return;
      } catch {
        // Fall through: the usual refresh shows the item and reports the history's own failure.
      }
    }
    this.itemResource.set(item);
    this.refreshHistory();
  }

  /** Fetches the item and its history again (after a conflict, or when a load failed). */
  reload(): void {
    this.itemResource.reload();
    this.refreshHistory();
  }

  reloadHistory(): void {
    this.historyResource.reload();
  }

  private refreshHistory(): void {
    // Newest first, so a new line is on page 1.
    if (this.query.values().page === 1) this.historyResource.reload();
    else this.query.setPage(1);
  }
}
