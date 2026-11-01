import { currencySymbol, formatMoney, formatMoneyInput, parseMoneyInput } from './money';
import { formatScaledDecimal, parseScaledDecimal } from './scaled-decimal';

describe('parseMoneyInput', () => {
  it.each([
    ['1,250.50', 125050],
    ['1250.5', 125050],
    ['1250', 125000],
    ['0.1', 10],
    ['0.10', 10],
    ['0.01', 1],
    ['.5', 50],
    ['0', 0],
    ['0.00', 0],
    ['  12.34  ', 1234],
    ['1,000,000', 100000000],
    ['₱1,250.50', 125050],
    ['1,250.', 125000],
  ])('reads %j as %d minor units', (text, expected) => {
    expect(parseMoneyInput(text)).toBe(expected);
  });

  it.each([
    ['1.234', 'more than two decimal places'],
    ['abc', 'letters'],
    ['', 'empty text'],
    ['   ', 'blank text'],
    ['.', 'a lone point'],
    ['1,25', 'a misplaced thousands separator'],
    ['12,34,567', 'uneven thousands groups'],
    ['1,2345', 'a long group'],
    ['1.2.3', 'two points'],
    ['1 250', 'a space inside'],
    ['12e3', 'an exponent'],
    ['-5', 'a negative when not allowed'],
    ['--5', 'a double minus'],
    ['1,250.5.0', 'a second point'],
  ])('rejects %j (%s)', (text) => {
    expect(parseMoneyInput(text)).toBeNull();
  });

  it('accepts a negative only when asked to', () => {
    expect(parseMoneyInput('-5', { allowNegative: true })).toBe(-500);
    expect(parseMoneyInput('-1,250.50', { allowNegative: true })).toBe(-125050);
    expect(parseMoneyInput('-0', { allowNegative: true })).toBe(0);
  });

  it('keeps large amounts exact and rejects what cannot be held exactly', () => {
    // 90,071,992,547,409.91 pesos is the largest amount a JavaScript number holds exactly in centavos.
    expect(parseMoneyInput('90,071,992,547,409.91')).toBe(Number.MAX_SAFE_INTEGER);
    expect(parseMoneyInput('90,071,992,547,409.92')).toBeNull();
    expect(parseMoneyInput('123,456,789,012.34')).toBe(12345678901234);
  });

  it('does not drift the way floating point does', () => {
    // 0.07 * 100 is 7.000000000000001 in floating point; reading digits gives exactly 7.
    expect(parseMoneyInput('0.07')).toBe(7);
    expect(parseMoneyInput('1.15')).toBe(115);
    expect(parseMoneyInput('19.99')).toBe(1999);
    expect(parseMoneyInput('4.35')).toBe(435);
    expect(parseMoneyInput('1,234,567.89')).toBe(123456789);
  });
});

describe('formatMoney', () => {
  it('writes the symbol, thousands separators and centavos', () => {
    expect(formatMoney(125050, 'PHP')).toBe('₱1,250.50');
    expect(formatMoney(125000, 'PHP')).toBe('₱1,250.00');
    expect(formatMoney(5, 'PHP')).toBe('₱0.05');
    expect(formatMoney(0, 'PHP')).toBe('₱0.00');
    expect(formatMoney(125000000000, 'PHP')).toBe('₱1,250,000,000.00');
  });

  it('can write centavos only when they are present', () => {
    expect(formatMoney(125000, 'PHP', { fraction: 'auto' })).toBe('₱1,250');
    expect(formatMoney(125050, 'PHP', { fraction: 'auto' })).toBe('₱1,250.50');
    expect(formatMoney(125010, 'PHP', { fraction: 'auto' })).toBe('₱1,250.10');
  });

  it('puts the minus sign before the symbol', () => {
    expect(formatMoney(-125050, 'PHP')).toBe('-₱1,250.50');
  });

  it('uses the symbol of the session currency', () => {
    expect(formatMoney(1999, 'USD')).toBe('$19.99');
    expect(currencySymbol('php')).toBe('₱');
  });

  it('falls back to the code for a currency it does not know', () => {
    expect(formatMoney(100, 'ZZZ')).toBe('ZZZ1.00');
  });

  it('round-trips with the parser', () => {
    for (const minor of [0, 1, 99, 100, 101, 125050, 99999999999]) {
      expect(parseMoneyInput(formatMoneyInput(minor))).toBe(minor);
    }
  });
});

describe('scaled decimals', () => {
  it('handles other scales', () => {
    expect(parseScaledDecimal('12.3456', 4)).toBe(123456);
    expect(parseScaledDecimal('12.34567', 4)).toBeNull();
    expect(parseScaledDecimal('7', 0)).toBe(7);
    expect(parseScaledDecimal('7.5', 0)).toBeNull();
    expect(formatScaledDecimal(123456, 4)).toBe('12.3456');
    expect(formatScaledDecimal(1200, 2, { trimZeros: true })).toBe('12');
    expect(formatScaledDecimal(1250, 2, { trimZeros: true, group: false })).toBe('12.5');
  });
});
