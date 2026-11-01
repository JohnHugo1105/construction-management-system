import { parseScaledDecimal } from '../../shared/presentation/scaled-decimal';

/** Quantities carry at most four decimal places, as the API accepts them. */
const QUANTITY_DECIMALS = 4;
const QUANTITY_SCALE = 10n ** BigInt(QUANTITY_DECIMALS);

/**
 * The amount a charged move will post to the project, in minor units: quantity times the item's unit cost, rounded once to the
 * centavo (halves away from zero), exactly as the server works it out. This is only a PREVIEW shown while the person fills in the
 * form; the saved figure always comes from the server.
 *
 * It works on digits only: the quantity text is read into a whole number of 1/10,000 units and multiplied as a BigInt, so a
 * floating-point error can never reach an amount. Returns null when the quantity is empty, zero, not a plain decimal, has more
 * than four decimals, or the result is too large to show exactly.
 */
export function chargePreviewMinor(
  quantity: string | number | null | undefined,
  unitCostMinor: number,
): number | null {
  if (quantity === null || quantity === undefined) return null;
  if (!Number.isSafeInteger(unitCostMinor) || unitCostMinor < 0) return null;

  // A number from the quantity box holds exactly the digits that were typed, so its shortest text is those digits again.
  const text = typeof quantity === 'number' ? String(quantity) : quantity;
  const scaled = parseScaledDecimal(text, QUANTITY_DECIMALS);
  if (scaled === null || scaled <= 0) return null;

  const product = BigInt(scaled) * BigInt(unitCostMinor);
  let minor = product / QUANTITY_SCALE;
  if ((product % QUANTITY_SCALE) * 2n >= QUANTITY_SCALE) minor += 1n;
  return minor > BigInt(Number.MAX_SAFE_INTEGER) ? null : Number(minor);
}
