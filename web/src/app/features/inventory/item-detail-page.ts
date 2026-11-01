import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { ToastService } from '../../core/notifications/toast-service';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { formatDate } from '../../shared/presentation/business-time';
import { formatMoney } from '../../shared/presentation/money';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Icon } from '../../shared/ui/icon/icon';
import { Menu } from '../../shared/ui/menu/menu';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Pager } from '../../shared/ui/pager/pager';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { BalanceChips } from './balance-chips';
import { CountStockDialog } from './count-stock-dialog';
import { CATEGORY, ITEM_STATUS, quantityWithUnit } from './inventory-presentation';
import { ITEM_ACTION_LABELS, ItemAction, itemActionPlan } from './item-actions';
import { ItemDetailState } from './item-detail-state';
import { InventoryItem, StockMovement } from './inventory.models';
import { MOVEMENT_TYPE, movementEnds, reverseCheck } from './movement-presentation';
import { MoveStockDialog } from './move-stock-dialog';
import { ReceiveStockDialog } from './receive-stock-dialog';
import { RetireItemDialog } from './retire-item-dialog';
import { ReverseMovementDialog } from './reverse-movement-dialog';
import { StockPlaces } from './stock-places';
import { WriteOffDialog } from './write-off-dialog';

type OpenDialog =
  | { readonly kind: Exclude<ItemAction, 'edit'> }
  | { readonly kind: 'reverse'; readonly movement: StockMovement };

/** The toast after each action worked. */
const DONE_MESSAGES: Record<OpenDialog['kind'], string> = {
  move: 'Stock moved.',
  receive: 'Stock received.',
  count: 'Count recorded.',
  'write-off': 'Write-off recorded.',
  retire: 'Item retired.',
  reverse: 'Movement reversed.',
};

/**
 * One item: what it is, where it is, and its history. People who manage stock get the one next step (usually "Move stock") with
 * the rest under "More"; everyone else reads. After any action the server's answer replaces the item and the history starts
 * again from its newest line, so the balances and the history always agree.
 */
@Component({
  selector: 'app-item-detail-page',
  imports: [
    RouterLink,
    PageHeader,
    Alert,
    ApiErrorAlert,
    Button,
    Icon,
    Menu,
    Pager,
    Skeleton,
    StatusChip,
    BalanceChips,
    MoveStockDialog,
    ReceiveStockDialog,
    CountStockDialog,
    WriteOffDialog,
    RetireItemDialog,
    ReverseMovementDialog,
  ],
  providers: [StockPlaces, ItemDetailState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './item-detail-page.html',
  styleUrl: './item-detail-page.scss',
})
export class ItemDetailPage {
  /** Bound from the route (`/stock/:id`). */
  readonly id = input.required<string>();

  protected readonly state = inject(ItemDetailState);
  protected readonly viewport = inject(Viewport);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);

  protected readonly canManage = this.session.permit(Permission.inventoryManage);
  protected readonly labels = ITEM_ACTION_LABELS;
  protected readonly dialog = signal<OpenDialog | null>(null);

  /** What is shown about the item, worked out once so the template only prints it. */
  protected readonly summary = computed(() => {
    const item = this.state.item();
    if (!item) return null;
    const currency = this.session.currencyCode();
    return {
      category: CATEGORY[item.category],
      status: ITEM_STATUS[item.status],
      about: [item.brand, item.serialNumber ? `Serial ${item.serialNumber}` : null]
        .filter(Boolean)
        .join(' · '),
      total: item.totalQuantity > 0 ? quantityWithUnit(item.totalQuantity, item.unit) : '',
      unitCost:
        item.unitCostMinor > 0
          ? `${formatMoney(item.unitCostMinor, currency)} each, including VAT`
          : 'No cost on record',
      purchased: formatDate(item.purchaseDate),
      plan: this.canManage() ? itemActionPlan(item) : { primary: null, more: [] },
    };
  });

  /** The history lines with their words worked out, and what can be done to each (only for people who manage stock). */
  protected readonly rows = computed(() => {
    const item = this.state.item();
    if (!item) return [];
    const currency = this.session.currencyCode();
    const canManage = this.canManage();
    return this.state.movements().map((movement) => {
      const ends = movementEnds(movement);
      const check = reverseCheck(movement, item.status);
      return {
        movement,
        type: MOVEMENT_TYPE[movement.type],
        date: formatDate(movement.date),
        from: ends.from,
        to: ends.to,
        quantity: quantityWithUnit(movement.quantity, item.unit),
        charged:
          movement.chargedAmountMinor > 0
            ? `Charged ${formatMoney(movement.chargedAmountMinor, currency)} to ${ends.to}`
            : '',
        // An offer to reverse, or the plain reason it cannot be (a reversal, or an item that is retired). A line that was
        // already reversed says so with its own "Reversed" marker.
        canReverse: canManage && check.allowed,
        whyNot: canManage && !check.allowed && !movement.isReversed ? check.reason : '',
      };
    });
  });

  /** The history line being reversed, when that dialog is open. */
  protected readonly reverseTarget = computed(() => {
    const open = this.dialog();
    return open?.kind === 'reverse' ? open.movement : null;
  });

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => this.state.id.set(id));
    });
  }

  protected open(action: ItemAction): void {
    if (action === 'edit') return;
    this.dialog.set({ kind: action });
  }

  protected reverse(movement: StockMovement): void {
    this.dialog.set({ kind: 'reverse', movement });
  }

  protected close(): void {
    this.dialog.set(null);
  }

  /** The server did it: take the item it answered with together with the newest history, then close and say so. */
  protected async done(kind: OpenDialog['kind'], item: InventoryItem): Promise<void> {
    await this.state.applied(item);
    this.dialog.set(null);
    this.toast.success(DONE_MESSAGES[kind]);
  }

  protected reload(): void {
    this.state.reload();
  }
}
