import { movement } from './inventory.fixtures';
import {
  MOVEMENT_TYPE,
  NOT_REVERSIBLE,
  movementEnds,
  reverseCheck,
  reversalSummary,
} from './movement-presentation';

describe('movementEnds', () => {
  it('names both places of a transfer', () => {
    expect(movementEnds(movement())).toEqual({ from: 'Warehouse', to: 'Riverside Residences' });
  });

  it('calls an outside side by what it is: a supplier, a stock count, a write-off', () => {
    expect(
      movementEnds(movement({ type: 'Purchase', fromName: null, toName: 'Warehouse' })),
    ).toEqual({ from: 'Supplier', to: 'Warehouse' });
    expect(
      movementEnds(movement({ type: 'Adjustment', fromName: null, toName: 'Warehouse' })).from,
    ).toBe('Stock count');
    expect(
      movementEnds(movement({ type: 'Adjustment', fromName: 'Warehouse', toName: null })).to,
    ).toBe('Stock count');
    expect(
      movementEnds(movement({ type: 'WriteOff', fromName: 'Warehouse', toName: null })).to,
    ).toBe('Written off');
  });

  it('has a word and a look for every kind of line', () => {
    expect(Object.values(MOVEMENT_TYPE).map((entry) => entry.label)).toEqual([
      'Purchase',
      'Transfer',
      'Count',
      'Write-off',
      'Reversal',
    ]);
  });
});

describe('reverseCheck: which lines can be reversed', () => {
  it('allows a transfer, a purchase, a count and a write-off that have not been reversed', () => {
    for (const type of ['Transfer', 'Purchase', 'Adjustment', 'WriteOff'] as const)
      expect(reverseCheck({ type, isReversed: false }, 'Active')).toEqual({ allowed: true });
  });

  it("refuses a reversal: it can't be reversed", () => {
    expect(reverseCheck({ type: 'Reversal', isReversed: false }, 'Active')).toEqual({
      allowed: false,
      reason: NOT_REVERSIBLE.reversal,
    });
  });

  it('refuses a line that was already reversed', () => {
    expect(reverseCheck({ type: 'Transfer', isReversed: true }, 'Active')).toEqual({
      allowed: false,
      reason: NOT_REVERSIBLE.alreadyReversed,
    });
  });

  it('on a retired item allows only the write-off', () => {
    expect(reverseCheck({ type: 'WriteOff', isReversed: false }, 'Retired')).toEqual({
      allowed: true,
    });
    expect(reverseCheck({ type: 'Purchase', isReversed: false }, 'Retired')).toEqual({
      allowed: false,
      reason: NOT_REVERSIBLE.retired,
    });
  });

  it('says a reversal is a reversal before it says anything about retirement', () => {
    expect(reverseCheck({ type: 'Reversal', isReversed: true }, 'Retired')).toMatchObject({
      allowed: false,
      reason: NOT_REVERSIBLE.reversal,
    });
  });
});

describe('reversalSummary', () => {
  const item = { unit: 'pc', status: 'Active' as const };

  it('says where a transfer goes back to', () => {
    expect(reversalSummary(movement({ quantity: 4 }), item, 'PHP')).toBe(
      'Moves 4 pc back from Riverside Residences to Warehouse.',
    );
  });

  it('says a charge to the project is cancelled too', () => {
    expect(
      reversalSummary(movement({ quantity: 4, chargedAmountMinor: 180000 }), item, 'PHP'),
    ).toBe(
      'Moves 4 pc back from Riverside Residences to Warehouse. The ₱1,800.00 charged to Riverside Residences is cancelled too.',
    );
  });

  it('says a write-off brings the item back to life when the item is retired', () => {
    const writeOff = movement({
      type: 'WriteOff',
      fromName: 'Warehouse',
      toName: null,
      quantity: 1,
    });
    expect(reversalSummary(writeOff, { unit: 'unit', status: 'Retired' }, 'PHP')).toBe(
      'Puts 1 unit back into Warehouse. The item is active again.',
    );
    expect(reversalSummary(writeOff, { unit: 'pc', status: 'Active' }, 'PHP')).toBe(
      'Puts 1 pc back into Warehouse.',
    );
  });
});
