import { formatScaledDecimal, parseScaledDecimal } from './scaled-decimal';

// Money crosses the API as whole minor units (centavos) plus a currency code. Every amount on screen goes through
// formatMoney, and every amount a person types goes through parseMoneyInput. The client never calculates money.

/** Minor units per major unit are 100 (centavos). The server stores every amount this way. */
const MINOR_DECIMALS = 2;

const symbolCache = new Map<string, string>();

/** The short symbol for a currency code: "PHP" gives "₱". Falls back to the code itself. */
export function currencySymbol(currencyCode: string): string {
  const code = currencyCode.trim().toUpperCase();
  const cached = symbolCache.get(code);
  if (cached !== undefined) return cached;

  let symbol = code;
  try {
    const part = new Intl.NumberFormat('en', {
      style: 'currency',
      currency: code,
      currencyDisplay: 'narrowSymbol',
    })
      .formatToParts(0)
      .find((item) => item.type === 'currency');
    if (part) symbol = part.value;
  } catch {
    // An unknown code is shown as typed.
  }
  symbolCache.set(code, symbol);
  return symbol;
}

export interface FormatMoneyOptions {
  /**
   * "always" writes the centavos every time ("₱1,250.00", the default, so a column lines up).
   * "auto" writes them only when present ("₱1,250" and "₱1,250.50").
   */
  readonly fraction?: 'always' | 'auto';
}

/** Formats minor units for display: 125050 and "PHP" give "₱1,250.50". */
export function formatMoney(
  minor: number,
  currencyCode: string,
  options: FormatMoneyOptions = {},
): string {
  const trimWhenWhole = options.fraction === 'auto' && minor % 100 === 0;
  const digits = formatScaledDecimal(Math.abs(minor), MINOR_DECIMALS, { trimZeros: trimWhenWhole });
  return `${minor < 0 ? '-' : ''}${currencySymbol(currencyCode)}${digits}`;
}

/** The amount alone, for an input to show again after editing: 125050 gives "1,250.50". */
export function formatMoneyInput(minor: number): string {
  return formatScaledDecimal(minor, MINOR_DECIMALS);
}

export interface ParseMoneyOptions {
  readonly allowNegative?: boolean;
}

/**
 * Reads typed money into minor units: "1,250.50" gives 125050, "0.1" gives 10. Returns null for empty text, anything
 * that is not an amount, more than two decimal places, or a value too large to hold exactly. A leading currency
 * symbol (pasted "₱1,250") is ignored.
 */
export function parseMoneyInput(text: string, options: ParseMoneyOptions = {}): number | null {
  const withoutSymbol = text.trim().replace(/^(-?)\s*\p{Sc}\s*/u, '$1');
  return parseScaledDecimal(withoutSymbol, MINOR_DECIMALS, options);
}
