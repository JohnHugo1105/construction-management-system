import { formatMoney } from '../../shared/presentation/money';
import { IconName } from '../../shared/ui/icon/icon';
import { ChipTone } from '../../shared/ui/status-chip/status-chip';
import { quantityWithUnit } from './inventory-presentation';
import { InventoryItemStatus, MovementType, StockMovement } from './inventory.models';

export interface MovementPresentation {
  readonly label: string;
  readonly tone: ChipTone;
  readonly icon: IconName;
}

/** How each kind of history line looks. The words are the ones a warehouse keeper uses: a "Count" is a stock count. */
export const MOVEMENT_TYPE: Record<MovementType, MovementPresentation> = {
  Purchase: { label: 'Purchase', tone: 'info', icon: 'plus' },
  Transfer: { label: 'Transfer', tone: 'neutral', icon: 'chevron-right' },
  Adjustment: { label: 'Count', tone: 'neutral', icon: 'sliders' },
  WriteOff: { label: 'Write-off', tone: 'danger', icon: 'minus-circle' },
  Reversal: { label: 'Reversal', tone: 'warning', icon: 'restore' },
};

/** What to call a side that is outside the system, by the kind of line it is on. */
const OUTSIDE: Record<MovementType, { readonly from: string; readonly to: string }> = {
  Purchase: { from: 'Supplier', to: 'Outside stock' },
  Transfer: { from: 'Outside stock', to: 'Outside stock' },
  Adjustment: { from: 'Stock count', to: 'Stock count' },
  WriteOff: { from: 'Outside stock', to: 'Written off' },
  Reversal: { from: 'Outside stock', to: 'Outside stock' },
};

/** Where a movement came from and where it went, by name. A missing name is a side outside the system. */
export function movementEnds(movement: StockMovement): { from: string; to: string } {
  return {
    from: movement.fromName ?? OUTSIDE[movement.type].from,
    to: movement.toName ?? OUTSIDE[movement.type].to,
  };
}

export type ReverseCheck =
  | { readonly allowed: true }
  | { readonly allowed: false; readonly reason: string };

/** Why a line cannot be reversed, in the same words the server uses. The server still decides when the person tries. */
export const NOT_REVERSIBLE = {
  reversal: "A reversal can't be reversed. Record the move again instead.",
  alreadyReversed: 'This was already reversed.',
  retired: 'This item is retired, so only its write-off can be reversed.',
} as const;

/**
 * Whether a line of an item's history can be reversed: not twice, not a reversal, and on a retired item only the write-off that
 * retired it. (Whether the stock is still where the line put it is for the server to say.)
 */
export function reverseCheck(
  movement: Pick<StockMovement, 'type' | 'isReversed'>,
  itemStatus: InventoryItemStatus,
): ReverseCheck {
  if (movement.type === 'Reversal') return { allowed: false, reason: NOT_REVERSIBLE.reversal };
  if (movement.isReversed) return { allowed: false, reason: NOT_REVERSIBLE.alreadyReversed };
  if (itemStatus === 'Retired' && movement.type !== 'WriteOff')
    return { allowed: false, reason: NOT_REVERSIBLE.retired };
  return { allowed: true };
}

/** What reversing a line will do, in a sentence, for the confirmation: "Moves 4 pc back from Riverside to the Warehouse." */
export function reversalSummary(
  movement: StockMovement,
  item: { readonly unit: string; readonly status: InventoryItemStatus },
  currencyCode: string,
): string {
  const amount = quantityWithUnit(movement.quantity, item.unit);
  const { from, to } = movementEnds(movement);
  const charge =
    movement.chargedAmountMinor > 0
      ? ` The ${formatMoney(movement.chargedAmountMinor, currencyCode)} charged to ${to} is cancelled too.`
      : '';
  switch (movement.type) {
    case 'Transfer':
      return `Moves ${amount} back from ${to} to ${from}.${charge}`;
    case 'Purchase':
      return `Takes ${amount} out of ${to}, as if it had never been received.`;
    case 'Adjustment':
      return 'Undoes the count: the stock goes back to what it was before.';
    case 'WriteOff':
      return `Puts ${amount} back into ${from}.${item.status === 'Retired' ? ' The item is active again.' : ''}`;
    case 'Reversal':
      return '';
  }
}
