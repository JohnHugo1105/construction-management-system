import { formatMoney } from '../../shared/presentation/money';
import { chargePreviewMinor } from './charge-preview';

describe('chargePreviewMinor', () => {
  it('multiplies a quantity by the unit cost: 14 x 450.00 is 6,300.00', () => {
    expect(chargePreviewMinor(14, 45000)).toBe(630000);
    expect(formatMoney(chargePreviewMinor(14, 45000)!, 'PHP')).toBe('₱6,300.00');
  });

  it('handles a large single piece: 1 x 4,850,000.00', () => {
    expect(chargePreviewMinor(1, 485000000)).toBe(485000000);
    expect(formatMoney(chargePreviewMinor(1, 485000000)!, 'PHP')).toBe('₱4,850,000.00');
  });

  it('handles a fraction of a unit: 0.5 x 3.00 is 1.50', () => {
    expect(chargePreviewMinor(0.5, 300)).toBe(150);
    expect(formatMoney(chargePreviewMinor(0.5, 300)!, 'PHP')).toBe('₱1.50');
  });

  it('rounds once to the centavo, halves away from zero, like the server', () => {
    // 0.5 x 5 centavos = 2.5 centavos, which rounds up to 3.
    expect(chargePreviewMinor(0.5, 5)).toBe(3);
    // 0.3333 x 1001 centavos = 333.6333, which rounds to 334.
    expect(chargePreviewMinor(0.3333, 1001)).toBe(334);
    // 2.4999 x 1 centavo = 2.4999, which rounds down to 2.
    expect(chargePreviewMinor(2.4999, 1)).toBe(2);
  });

  it('is exact where floating point is not: 1.005 x 100 centavos is 100.5, so 101', () => {
    // In floating point 1.005 * 100 is 100.49999999999999 and would round the wrong way.
    expect(1.005 * 100).toBeLessThan(100.5);
    expect(chargePreviewMinor(1.005, 100)).toBe(101);
    expect(chargePreviewMinor('4.35', 100)).toBe(435);
    expect(chargePreviewMinor('0.07', 100)).toBe(7);
  });

  it('reads the digits a person typed, commas included', () => {
    expect(chargePreviewMinor('1,200', 100)).toBe(120000);
    expect(chargePreviewMinor('.5', 300)).toBe(150);
  });

  it('works out to zero when the quantity is too small to charge a centavo', () => {
    expect(chargePreviewMinor(0.0001, 1)).toBe(0);
  });

  it('gives nothing for a quantity that is empty, zero, negative or not a plain number', () => {
    expect(chargePreviewMinor(null, 45000)).toBeNull();
    expect(chargePreviewMinor(undefined, 45000)).toBeNull();
    expect(chargePreviewMinor('', 45000)).toBeNull();
    expect(chargePreviewMinor(0, 45000)).toBeNull();
    expect(chargePreviewMinor('-2', 45000)).toBeNull();
    expect(chargePreviewMinor('abc', 45000)).toBeNull();
    expect(chargePreviewMinor('1.23456', 45000)).toBeNull();
  });

  it('gives nothing when the unit cost is not a valid amount of centavos', () => {
    expect(chargePreviewMinor(2, -1)).toBeNull();
    expect(chargePreviewMinor(2, 1.5)).toBeNull();
  });

  it('never hands back an amount too large to show exactly', () => {
    expect(chargePreviewMinor('999999999', Number.MAX_SAFE_INTEGER)).toBeNull();
  });
});
