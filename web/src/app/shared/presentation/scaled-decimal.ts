// Decimal text to a scaled integer and back, by digits only. No floating point is ever involved, so 0.1 + 0.2 style drift
// cannot reach a money amount: "1,250.50" becomes the integer 125050 by reading its digits, never by multiplying a float.

const LARGEST_SAFE = BigInt(Number.MAX_SAFE_INTEGER);

export interface ParseScaledOptions {
  /** Allow a leading minus sign. Off by default: most amounts here cannot be negative. */
  readonly allowNegative?: boolean;
}

/**
 * Reads what a person types ("1,250.50", ".5", "12") into a whole number of 1/10^decimals units.
 * Returns null when the text is empty, is not a plain decimal, has more decimal places than allowed, uses thousands
 * separators in the wrong places, or is too large to hold exactly.
 */
export function parseScaledDecimal(
  text: string,
  decimals: number,
  options: ParseScaledOptions = {},
): number | null {
  let rest = text.trim();
  if (rest === '') return null;

  let negative = false;
  if (rest.startsWith('-')) {
    if (!options.allowNegative) return null;
    negative = true;
    rest = rest.slice(1);
  }

  const match = /^(\d{1,3}(?:,\d{3})+|\d+)?(?:\.(\d*))?$/.exec(rest);
  if (!match) return null;

  const whole = (match[1] ?? '').replaceAll(',', '');
  const fraction = match[2] ?? '';
  if (whole === '' && fraction === '') return null;
  if (fraction.length > decimals) return null;

  const scale = 10n ** BigInt(decimals);
  const scaled =
    BigInt(whole === '' ? '0' : whole) * scale + BigInt(fraction.padEnd(decimals, '0') || '0');
  if (scaled > LARGEST_SAFE) return null;

  const value = Number(scaled);
  return negative && value !== 0 ? -value : value;
}

export interface FormatScaledOptions {
  /** Put a comma between thousands. On by default. */
  readonly group?: boolean;
  /** Drop trailing zeros after the point (12.50 becomes 12.5, 12.00 becomes 12). */
  readonly trimZeros?: boolean;
}

/** Writes a whole number of 1/10^decimals units as text ("125050" with 2 decimals becomes "1,250.50"). */
export function formatScaledDecimal(
  value: number,
  decimals: number,
  options: FormatScaledOptions = {},
): string {
  const { group = true, trimZeros = false } = options;
  if (!Number.isFinite(value)) return '';

  const rounded = Math.round(value);
  const absolute = BigInt(Math.abs(rounded));
  const scale = 10n ** BigInt(decimals);
  const whole = (absolute / scale).toString();
  let fraction = decimals > 0 ? (absolute % scale).toString().padStart(decimals, '0') : '';
  if (trimZeros) fraction = fraction.replace(/0+$/, '');

  const grouped = group ? whole.replace(/\B(?=(\d{3})+(?!\d))/g, ',') : whole;
  return `${rounded < 0 ? '-' : ''}${grouped}${fraction === '' ? '' : `.${fraction}`}`;
}
