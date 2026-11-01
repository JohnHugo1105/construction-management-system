import { formatBps, formatBpsInput, parsePercentInput } from './percent';

describe('percent and basis points', () => {
  it('reads a typed percent as basis points', () => {
    expect(parsePercentInput('12')).toBe(1200);
    expect(parsePercentInput('12.5')).toBe(1250);
    expect(parsePercentInput('12.5%')).toBe(1250);
    expect(parsePercentInput('125')).toBe(12500);
    expect(parsePercentInput('0')).toBe(0);
    expect(parsePercentInput('0.01')).toBe(1);
  });

  it('rejects what is not a percent', () => {
    expect(parsePercentInput('')).toBeNull();
    expect(parsePercentInput('twelve')).toBeNull();
    expect(parsePercentInput('12.345')).toBeNull();
    expect(parsePercentInput('-3')).toBeNull();
  });

  it('writes basis points as a percent without trailing zeros', () => {
    expect(formatBps(1200)).toBe('12%');
    expect(formatBps(1250)).toBe('12.5%');
    expect(formatBps(12500)).toBe('125%');
    expect(formatBpsInput(1250)).toBe('12.5');
  });
});
