import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Pager } from '../../shared/ui/pager/pager';
import { SearchBox } from '../../shared/ui/search-box/search-box';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { ReceivingListState } from './receiving-list-state';
import { expectedOn, placeOf, presentDelivery } from './receiving-presentation';

/**
 * The deliveries still to come: one card per approved order that has something left to receive, soonest expected first. Each card
 * says who is delivering, where to, when, and leads to one next step, Open. Quantities only: no amount appears on this page.
 */
@Component({
  selector: 'app-receiving-page',
  imports: [
    RouterLink,
    PageHeader,
    SearchBox,
    Button,
    StatusChip,
    Pager,
    EmptyState,
    Skeleton,
    ApiErrorAlert,
  ],
  providers: [ReceivingListState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './receiving-page.html',
  styleUrl: './receiving-page.scss',
})
export class ReceivingPage {
  protected readonly state = inject(ReceivingListState);

  /** Nothing to search yet: a search box for an empty list would only get in the way. */
  protected readonly bare = computed(
    () =>
      !this.state.loadingFirst() &&
      this.state.items().length === 0 &&
      !this.state.query.isFiltered() &&
      this.state.error() === null,
  );

  protected readonly rows = computed(() =>
    this.state.items().map((order) => ({
      id: order.id,
      number: order.number,
      supplier: order.supplierName,
      place: placeOf(order),
      expected: expectedOn(order.expectedDate),
      delivery: presentDelivery(order.deliveryState),
    })),
  );
}
