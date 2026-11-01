import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Icon } from '../../shared/ui/icon/icon';
import { balanceKey } from './stock-places';
import { balanceName, formatQuantity } from './inventory-presentation';
import { StockBalance } from './inventory.models';

/**
 * Where an item is, as compact chips: "Warehouse 14", "Riverside Residences 10". Only places that hold some are listed, so there
 * are never zero chips; an item with no stock anywhere says so in words. The unit follows the number when asked for.
 */
@Component({
  selector: 'app-balance-chips',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (chips().length > 0) {
      <ul class="places" [attr.aria-label]="label()">
        @for (chip of chips(); track chip.key) {
          <li class="place">
            <app-icon [name]="chip.icon" [size]="14" />
            <span class="place__text"
              >{{ chip.name }} <strong class="place__quantity">{{ chip.quantity }}</strong></span
            >
          </li>
        }
      </ul>
    } @else {
      <p class="places__none">No stock anywhere</p>
    }
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }

    .places {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-1) var(--space-2);
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .place {
      display: inline-flex;
      align-items: flex-start;
      gap: 6px;
      max-width: 100%;
      min-height: 24px;
      padding: 2px 10px 2px 8px;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-pill);
      background: var(--color-surface-2);
      color: var(--color-text);
      font-size: var(--text-xs);
      line-height: 1.3;
    }

    .place app-icon {
      flex: none;
      margin-top: 2px;
      color: var(--color-text-muted);
    }

    .place__text {
      min-width: 0;
      overflow-wrap: anywhere;
    }

    .place__quantity {
      font-weight: 700;
      font-variant-numeric: tabular-nums;
      white-space: nowrap;
    }

    .places__none {
      font-size: var(--text-sm);
      color: var(--color-text-muted);
    }
  `,
})
export class BalanceChips {
  readonly balances = input.required<readonly StockBalance[]>();
  /** The unit to write after each number ("pc"); leave empty in a crowded row. */
  readonly unit = input('');
  readonly label = input('Where it is');

  protected readonly chips = computed(() =>
    this.balances().map((balance) => ({
      key: balanceKey(balance),
      icon: balance.location === 'Warehouse' ? ('stock' as const) : ('building' as const),
      name: balanceName(balance),
      quantity: this.unit()
        ? `${formatQuantity(balance.quantity)} ${this.unit()}`
        : formatQuantity(balance.quantity),
    })),
  );
}
