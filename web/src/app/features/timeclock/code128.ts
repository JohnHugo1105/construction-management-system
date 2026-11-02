/**
 * Code 128, code set B: the barcode most handheld scanners read. A badge code (12 upper-case letters and digits) is one Start B symbol, one
 * symbol per character (its ASCII code less 32), a check symbol, and the Stop symbol. Each symbol is three bars and three spaces of 1 to 4
 * modules (11 modules in all); the Stop symbol has a fourth bar (13 modules). Drawn here, in SVG, so no library and no network is needed.
 */

/** The bar and space widths of every symbol value, 0 to 106, from the Code 128 specification (ISO/IEC 15417). */
export const CODE128_WIDTHS: readonly string[] = [
  '212222',
  '222122',
  '222221',
  '121223',
  '121322',
  '131222',
  '122213',
  '122312',
  '132212',
  '221213',
  '221312',
  '231212',
  '112232',
  '122132',
  '122231',
  '113222',
  '123122',
  '123221',
  '223211',
  '221132',
  '221231',
  '213212',
  '223112',
  '312131',
  '311222',
  '321122',
  '321221',
  '312212',
  '322112',
  '322211',
  '212123',
  '212321',
  '232121',
  '111323',
  '131123',
  '131321',
  '112313',
  '132113',
  '132311',
  '211313',
  '231113',
  '231311',
  '112133',
  '112331',
  '132131',
  '113123',
  '113321',
  '133121',
  '313121',
  '211331',
  '231131',
  '213113',
  '213311',
  '213131',
  '311123',
  '311321',
  '331121',
  '312113',
  '312311',
  '332111',
  '314111',
  '221411',
  '431111',
  '111224',
  '111422',
  '121124',
  '121421',
  '141122',
  '141221',
  '112214',
  '112412',
  '122114',
  '122411',
  '142112',
  '142211',
  '241211',
  '221114',
  '413111',
  '241112',
  '134111',
  '111242',
  '121142',
  '121241',
  '114212',
  '124112',
  '124211',
  '411212',
  '421112',
  '421211',
  '212141',
  '214121',
  '412121',
  '111143',
  '111341',
  '131141',
  '114113',
  '114311',
  '411113',
  '411311',
  '113141',
  '114131',
  '311141',
  '411131',
  '211412',
  '211214',
  '211232',
  '2331112',
];

export const START_B = 104;
export const STOP = 106;

/** The number of blank modules on each side of the bars, so a scanner finds where the code starts and ends. */
export const QUIET_ZONE = 10;

/** The symbol values of a text in code set B: Start B, one per character, the check symbol, Stop. Null for a character set B can't hold. */
export function code128Values(text: string): number[] | null {
  const values = [START_B];
  for (const character of text) {
    const code = character.charCodeAt(0);
    if (code < 32 || code > 126) return null;
    values.push(code - 32);
  }
  // The check symbol: the start value plus each symbol's value times its position (1, 2, ...), modulo 103.
  let sum = START_B;
  for (let position = 1; position < values.length; position++) sum += values[position]! * position;
  values.push(sum % 103);
  values.push(STOP);
  return values;
}

export interface BarcodeBar {
  /** Where the bar starts, in modules from the left edge (the quiet zone included). */
  readonly x: number;
  readonly width: number;
}

export interface BarcodeDrawing {
  /** The total width in modules, quiet zones included. */
  readonly modules: number;
  readonly bars: readonly BarcodeBar[];
}

/** The bars of a text, ready to draw as rectangles one module wide per unit. Null when the text can't be encoded. */
export function code128Bars(text: string): BarcodeDrawing | null {
  const values = code128Values(text);
  if (!values) return null;
  const bars: BarcodeBar[] = [];
  let x = QUIET_ZONE;
  for (const value of values) {
    const widths = CODE128_WIDTHS[value]!;
    for (let index = 0; index < widths.length; index++) {
      const width = Number(widths[index]);
      // Even positions are bars, odd positions are spaces.
      if (index % 2 === 0) bars.push({ x, width });
      x += width;
    }
  }
  return { modules: x + QUIET_ZONE, bars };
}
