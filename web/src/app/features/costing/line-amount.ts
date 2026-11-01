// The live "Line total" preview in the entry forms: quantity times unit cost, worked out by digits only (BigInt), so a value
// like 0.1 x 3 or 123.4567 x 5,800.00 is exact and never drifts the way floating point does. It is a preview only: the saved
// net, VAT and total always come from the server's response.

const QUANTITY_PLACES = 4;
const QUANTITY_SCALE = 10n ** BigInt(QUANTITY_PLACES);
const LARGEST_SAFE = BigInt(Number.MAX_SAFE_INTEGER);

/** The quantity as a whole number of 1/10,000 units, read from its exact decimal text; null when it is not usable. */
function scaledQuantity(quantity: number): bigint | null {
  if (!Number.isFinite(quantity) || quantity <= 0) return null;
  const text = quantity.toFixed(QUANTITY_PLACES);
  // toFixed switches to exponent notation from 1e21: far beyond anything the server accepts.
  if (!/^\d+\.\d+$/.test(text)) return null;
  return BigInt(text.replace('.', ''));
}

/**
 * Quantity x unit cost in whole minor units (centavos), rounded half away from zero as the server does. Returns null while
 * either value is missing or unusable (empty, zero or negative quantity, a negative cost) or when the result is too large to
 * show exactly.
 */
export function lineTotalMinor(
  quantity: number | null | undefined,
  unitCostMinor: number | null | undefined,
): number | null {
  if (quantity === null || quantity === undefined) return null;
  if (unitCostMinor === null || unitCostMinor === undefined) return null;
  if (!Number.isSafeInteger(unitCostMinor) || unitCostMinor < 0) return null;

  const scaled = scaledQuantity(quantity);
  if (scaled === null) return null;

  const product = scaled * BigInt(unitCostMinor);
  let whole = product / QUANTITY_SCALE;
  if ((product % QUANTITY_SCALE) * 2n >= QUANTITY_SCALE) whole += 1n;
  return whole > LARGEST_SAFE ? null : Number(whole);
}
