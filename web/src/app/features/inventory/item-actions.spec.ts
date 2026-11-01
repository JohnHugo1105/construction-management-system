import { excavator, hammers } from './inventory.fixtures';
import { ITEM_ACTION_LABELS, itemActionPlan } from './item-actions';

describe('itemActionPlan', () => {
  it('leads with Move stock for a tool that has stock, and folds the rest away', () => {
    expect(itemActionPlan(hammers())).toEqual({
      primary: 'move',
      more: ['receive', 'count', 'write-off', 'retire', 'edit'],
    });
  });

  it('never offers to restock a single piece', () => {
    const plan = itemActionPlan(excavator());

    expect(plan.primary).toBe('move');
    expect(plan.more).toEqual(['count', 'write-off', 'retire', 'edit']);
  });

  it('offers nothing at all on a retired item', () => {
    expect(itemActionPlan(hammers({ status: 'Retired', totalQuantity: 0, balances: [] }))).toEqual({
      primary: null,
      more: [],
    });
    expect(
      itemActionPlan(excavator({ status: 'Retired', totalQuantity: 0, balances: [] })),
    ).toEqual({
      primary: null,
      more: [],
    });
  });

  it('restocks a tool with none left, and counts a single piece that is nowhere (it may be found)', () => {
    const none = { totalQuantity: 0, balances: [] };

    expect(itemActionPlan(hammers(none))).toEqual({
      primary: 'receive',
      more: ['count', 'retire', 'edit'],
    });
    expect(itemActionPlan(excavator(none))).toEqual({ primary: 'count', more: ['retire', 'edit'] });
  });

  it('names every action after its result', () => {
    expect(Object.values(ITEM_ACTION_LABELS)).toEqual([
      'Move stock',
      'Receive more stock',
      'Count stock',
      'Write off',
      'Retire item',
      'Edit details',
    ]);
  });
});
