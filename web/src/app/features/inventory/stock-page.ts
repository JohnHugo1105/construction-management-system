import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Permission } from '../../core/session/permissions';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { Icon } from '../../shared/ui/icon/icon';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Pager } from '../../shared/ui/pager/pager';
import { SearchBox } from '../../shared/ui/search-box/search-box';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { SessionState } from '../../core/session/session-state';
import { BalanceChips } from './balance-chips';
import {
  CATEGORY,
  CATEGORY_FILTERS,
  ITEM_STATUS,
  categoryFilterLabel,
  quantityWithUnit,
} from './inventory-presentation';
import { StockListState } from './stock-list-state';
import { StockPlaces } from './stock-places';

/**
 * The stock list: search, category chips, where it is, a table on wide screens and cards on phones, paged by the server. Active
 * items are listed; retired ones sit behind "Show retired". Managers get the one next step, "Add an item".
 */
@Component({
  selector: 'app-stock-page',
  imports: [
    RouterLink,
    PageHeader,
    SearchBox,
    Button,
    Icon,
    StatusChip,
    Pager,
    EmptyState,
    Skeleton,
    ApiErrorAlert,
    BalanceChips,
  ],
  providers: [StockPlaces, StockListState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './stock-page.html',
  styleUrl: './stock-page.scss',
})
export class StockPage {
  protected readonly state = inject(StockListState);
  protected readonly viewport = inject(Viewport);
  private readonly session = inject(SessionState);
  private readonly places = inject(StockPlaces);

  protected readonly canManage = this.session.permit(Permission.inventoryManage);
  protected readonly filters = CATEGORY_FILTERS;
  protected readonly filterLabel = categoryFilterLabel;

  constructor() {
    this.places.load();
  }

  /** The rows with their words worked out, so the template only prints them. */
  protected readonly rows = computed(() =>
    this.state.items().map((item) => ({
      id: item.id,
      description: item.description,
      category: CATEGORY[item.category],
      retired: item.status === 'Retired' ? ITEM_STATUS.Retired : null,
      subtitle: [item.brand, item.serialNumber ? `Serial ${item.serialNumber}` : null]
        .filter(Boolean)
        .join(' · '),
      // An item with nothing left shows "No stock anywhere" in the places column instead of a zero.
      quantity: item.totalQuantity > 0 ? quantityWithUnit(item.totalQuantity, item.unit) : '',
      balances: item.balances,
    })),
  );

  protected placeChanged(event: Event): void {
    this.state.query.set({ place: (event.target as HTMLSelectElement).value });
  }

  protected retiredChanged(event: Event): void {
    this.state.query.set({ retired: (event.target as HTMLInputElement).checked });
  }
}
