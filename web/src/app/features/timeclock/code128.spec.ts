import { TestBed } from '@angular/core/testing';
import { encode } from 'uqr';
import { BadgeCard, groupedCode } from './badge-card';
import { CODE128_WIDTHS, QUIET_ZONE, START_B, STOP, code128Bars, code128Values } from './code128';
import { settle } from '../../testing/dom';

/** The bars of a drawing as 1 (bar) and 0 (space) per module, without the quiet zones: how references write a Code 128 symbol. */
function modules(text: string): string {
  const drawing = code128Bars(text)!;
  const cells = new Array<string>(drawing.modules - 2 * QUIET_ZONE).fill('0');
  for (const bar of drawing.bars)
    for (let x = bar.x; x < bar.x + bar.width; x++) cells[x - QUIET_ZONE] = '1';
  return cells.join('');
}

describe('Code 128 (code set B)', () => {
  it('has the 107 symbols of the specification: 11 modules each, 13 for Stop, all different', () => {
    expect(CODE128_WIDTHS).toHaveLength(107);
    CODE128_WIDTHS.slice(0, STOP).forEach((widths) => {
      expect(widths).toHaveLength(6);
      expect([...widths].reduce((sum, width) => sum + Number(width), 0)).toBe(11);
    });
    expect([...CODE128_WIDTHS[STOP]!].reduce((sum, width) => sum + Number(width), 0)).toBe(13);
    expect(new Set(CODE128_WIDTHS).size).toBe(107);
  });

  it('encodes a badge code as Start B, one symbol per character, the check symbol and Stop', () => {
    // A = 33, B = 34 ...: the check is (104 + 33x1 + 34x2 + ...) mod 103.
    const values = code128Values('ABCDEFGHJKMN')!;
    expect(values[0]).toBe(START_B);
    expect(values.slice(1, 13)).toEqual([33, 34, 35, 36, 37, 38, 39, 40, 42, 43, 45, 46]);
    const sum =
      104 + values.slice(1, 13).reduce((total, value, index) => total + value * (index + 1), 0);
    expect(values[13]).toBe(sum % 103);
    expect(values[14]).toBe(STOP);
  });

  // Reference encodings from an independent Code 128 implementation (JsBarcode 3.11.6, code set B), bar = 1 and space = 0.
  it.each([
    [
      'ABCDEFGHJKMN',
      '11010010000101000110001000101100010001000110101100010001000110100010001100010110100010001100010100010110111000101100011101011101100010111000110110010111001100011101011',
    ],
    [
      '23456789ABCD',
      '11010010000110011100101100101110011001001110110111001001100111010011101101110111010011001110010110010100011000100010110001000100011010110001000100010011001100011101011',
    ],
    [
      'Wikipedia',
      '11010010000111010001101000011010011000010010100001101001010011110010110010000100001001101000011010010010110000111100100101100011101011',
    ],
  ])('draws %s exactly as the reference does', (text, expected) => {
    expect(modules(text)).toBe(expected);
  });

  it('keeps ten blank modules on each side and refuses a character code set B cannot hold', () => {
    const drawing = code128Bars('ABCDEFGHJKMN')!;
    expect(drawing.bars[0]!.x).toBe(QUIET_ZONE);
    const last = drawing.bars[drawing.bars.length - 1]!;
    expect(drawing.modules - (last.x + last.width)).toBe(QUIET_ZONE);
    expect(code128Bars('café')).toBeNull();
    expect(code128Values('tab\there')).toBeNull();
  });
});

describe('BadgeCard', () => {
  it('shows the company, name, employee number, job title, a barcode and a QR code of the code, and the code in groups of four', async () => {
    const fixture = TestBed.createComponent(BadgeCard);
    fixture.componentRef.setInput('company', 'Acme Builders');
    fixture.componentRef.setInput('badge', {
      workerId: 'w1',
      fullName: 'Juan Dela Cruz',
      employeeNumber: 'W-001',
      jobTitle: 'Foreman',
      code: 'ABCDEFGHJKMN',
    });
    await settle(fixture);
    const root = fixture.nativeElement as HTMLElement;

    expect(root.querySelector('.badge-card__company')!.textContent).toContain('Acme Builders');
    expect(root.querySelector('.badge-card__name')!.textContent).toContain('Juan Dela Cruz');
    expect(root.querySelector('.badge-card__about')!.textContent).toContain('W-001 · Foreman');
    expect(root.querySelector('.badge-card__code')!.textContent).toContain('ABCD-EFGH-JKMN');

    const barcode = root.querySelector('svg.code128')!;
    expect(barcode.getAttribute('aria-label')).toBe('Barcode of ABCDEFGHJKMN');
    expect(barcode.querySelectorAll('rect[fill="#000000"]')).toHaveLength(
      code128Bars('ABCDEFGHJKMN')!.bars.length,
    );

    const qr = root.querySelector('svg.qr')!;
    expect(qr.getAttribute('aria-label')).toBe('QR code of ABCDEFGHJKMN');
    const size = encode('ABCDEFGHJKMN', { ecc: 'M', border: 2 }).size;
    expect(qr.getAttribute('viewBox')).toBe(`0 0 ${size} ${size}`);
    expect(qr.querySelector('path')!.getAttribute('d')!.length).toBeGreaterThan(100);
    // Nothing about pay on a badge.
    expect(root.textContent).not.toMatch(/₱|rate|pay/i);
  });

  it('prints no symbols for a worker without a code', async () => {
    const fixture = TestBed.createComponent(BadgeCard);
    fixture.componentRef.setInput('company', 'Acme Builders');
    fixture.componentRef.setInput('badge', {
      workerId: 'w1',
      fullName: 'Ana',
      employeeNumber: 'W-2',
      jobTitle: null,
      code: null,
    });
    await settle(fixture);

    expect((fixture.nativeElement as HTMLElement).querySelector('svg')).toBeNull();
    expect(groupedCode('23456789ABCD')).toBe('2345-6789-ABCD');
  });
});
