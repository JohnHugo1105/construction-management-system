import { lineTotalMinor } from './line-amount';

describe('lineTotalMinor (the live "Line total" preview)', () => {
  it('multiplies quantity by unit cost exactly: 123.4567 x 5,800.00', () => {
    // 123.4567 x 580,000 centavos = 71,604,886 centavos, which is 716,048.86 pesos.
    expect(lineTotalMinor(123.4567, 580000)).toBe(71604886);
  });

  it('shows no floating-point drift where doubles do', () => {
    // 0.1 x 3 in doubles is 0.30000000000000004; 1.1 x 100 is 110.00000000000001.
    expect(0.1 * 3).not.toBe(0.3);
    expect(lineTotalMinor(0.1, 3)).toBe(0); // 0.3 centavo rounds down
    expect(lineTotalMinor(0.1, 300)).toBe(30);
    expect(lineTotalMinor(1.1, 100)).toBe(110);
    expect(lineTotalMinor(0.7, 1000)).toBe(700);
    expect(lineTotalMinor(4.35, 100)).toBe(435); // 4.35 * 100 is 434.99999999999994 in doubles
  });

  it('rounds a part of a centavo half away from zero, as the server does', () => {
    expect(lineTotalMinor(0.5, 1)).toBe(1); // 0.5 centavo rounds up
    expect(lineTotalMinor(0.4999, 1)).toBe(0);
    expect(lineTotalMinor(1.5, 1)).toBe(2);
    expect(lineTotalMinor(2.5, 1)).toBe(3);
    expect(lineTotalMinor(0.0001, 5000)).toBe(1); // 0.5 centavo
  });

  it('keeps every digit of very large values', () => {
    // The product has sixteen digits: a double would round it, so the digits are multiplied as integers.
    expect(lineTotalMinor(12345678.9012, 100000000)).toBe(1234567890120000);
    expect(lineTotalMinor(999999999.9999, 9007199)).toBe(9007198999999099);
    // Beyond the largest amount that can be shown exactly, nothing is shown rather than a rounded figure.
    expect(lineTotalMinor(999999999.9999, 9007200)).toBe(null);
  });

  it('gives nothing while a value is missing or unusable', () => {
    expect(lineTotalMinor(null, 100)).toBe(null);
    expect(lineTotalMinor(2, null)).toBe(null);
    expect(lineTotalMinor(undefined, undefined)).toBe(null);
    expect(lineTotalMinor(0, 100)).toBe(null);
    expect(lineTotalMinor(-1, 100)).toBe(null);
    expect(lineTotalMinor(2, -100)).toBe(null);
    expect(lineTotalMinor(Number.NaN, 100)).toBe(null);
    expect(lineTotalMinor(2, 1.5)).toBe(null);
  });

  it('handles a zero unit cost and whole quantities', () => {
    expect(lineTotalMinor(5, 0)).toBe(0);
    expect(lineTotalMinor(600, 580000)).toBe(348000000);
    expect(lineTotalMinor(42000, 5200)).toBe(218400000);
  });
});
