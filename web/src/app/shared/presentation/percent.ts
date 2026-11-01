import { formatScaledDecimal, parseScaledDecimal } from './scaled-decimal';

// Rates cross the API as basis points: 1,200 is 12%, 12,500 is 125%. People type and read percents.

const PERCENT_DECIMALS = 2;

/** 1200 gives "12%", 1250 gives "12.5%", 12500 gives "125%". */
export function formatBps(bps: number): string {
  return `${formatScaledDecimal(bps, PERCENT_DECIMALS, { trimZeros: true })}%`;
}

/** The number alone for an input: 1250 gives "12.5". */
export function formatBpsInput(bps: number): string {
  return formatScaledDecimal(bps, PERCENT_DECIMALS, { trimZeros: true, group: false });
}

/** Reads a typed percent into basis points: "12" gives 1200, "12.5%" gives 1250. Null when it is not a percent. */
export function parsePercentInput(text: string): number | null {
  return parseScaledDecimal(text.trim().replace(/\s*%$/, ''), PERCENT_DECIMALS);
}
