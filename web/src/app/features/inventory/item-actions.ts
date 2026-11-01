import { isSinglePiece } from './inventory-presentation';
import { InventoryItem } from './inventory.models';

/** What a person can do to an item as a whole (reversing a line of its history is offered on that line). */
export type ItemAction = 'move' | 'receive' | 'count' | 'write-off' | 'retire' | 'edit';

export const ITEM_ACTION_LABELS: Record<ItemAction, string> = {
  move: 'Move stock',
  receive: 'Receive more stock',
  count: 'Count stock',
  'write-off': 'Write off',
  retire: 'Retire item',
  edit: 'Edit details',
};

export interface ItemActionPlan {
  /** The one next step, shown as the page's primary button. Null when nothing can be done. */
  readonly primary: ItemAction | null;
  /** Everything else that is allowed, folded into the "More" menu. */
  readonly more: readonly ItemAction[];
}

/**
 * What to offer for an item. A retired item offers nothing (it can't be edited, moved or restocked). With stock on record the
 * next step is to move it; with none, a tool is restocked and a single piece is counted (it may have been found). Writing off needs
 * stock to write off, and a single piece is never restocked. The server enforces the same rules; this decides what to offer.
 */
export function itemActionPlan(
  item: Pick<InventoryItem, 'status' | 'category' | 'totalQuantity'>,
): ItemActionPlan {
  if (item.status === 'Retired') return { primary: null, more: [] };

  const tool = !isSinglePiece(item.category);
  if (item.totalQuantity > 0)
    return {
      primary: 'move',
      more: [...(tool ? (['receive'] as const) : []), 'count', 'write-off', 'retire', 'edit'],
    };

  return {
    primary: tool ? 'receive' : 'count',
    more: [...(tool ? (['count'] as const) : []), 'retire', 'edit'],
  };
}
