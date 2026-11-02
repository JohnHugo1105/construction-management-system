import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { encode } from 'uqr';
import { code128Bars } from './code128';

/** A Code 128 barcode of a badge code, drawn as SVG rectangles (one unit per module, quiet zones included). */
@Component({
  selector: 'app-code128',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (drawing(); as barcode) {
      <svg
        class="code128"
        [attr.viewBox]="'0 0 ' + barcode.modules + ' 30'"
        preserveAspectRatio="none"
        shape-rendering="crispEdges"
        role="img"
        [attr.aria-label]="'Barcode of ' + value()"
      >
        <rect [attr.width]="barcode.modules" height="30" fill="#ffffff" />
        @for (bar of barcode.bars; track $index) {
          <rect [attr.x]="bar.x" y="0" [attr.width]="bar.width" height="30" fill="#000000" />
        }
      </svg>
    }
  `,
  styles: `
    :host {
      display: block;
    }

    .code128 {
      display: block;
      width: 100%;
      height: 100%;
    }
  `,
})
export class Code128 {
  readonly value = input.required<string>();
  protected readonly drawing = computed(() => code128Bars(this.value()));
}

/**
 * A QR code of a badge code, from the bundled uqr library (MIT, pinned), drawn here as one SVG path so it prints crisply and nothing is fetched.
 * Medium error correction keeps it readable through scuffs on a card worn on site.
 */
@Component({
  selector: 'app-qr-code',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <svg
      class="qr"
      [attr.viewBox]="'0 0 ' + qr().size + ' ' + qr().size"
      shape-rendering="crispEdges"
      role="img"
      [attr.aria-label]="'QR code of ' + value()"
    >
      <rect [attr.width]="qr().size" [attr.height]="qr().size" fill="#ffffff" />
      <path [attr.d]="qr().path" fill="#000000" />
    </svg>
  `,
  styles: `
    :host {
      display: block;
    }

    .qr {
      display: block;
      width: 100%;
      height: 100%;
    }
  `,
})
export class QrCode {
  readonly value = input.required<string>();

  protected readonly qr = computed(() => {
    const result = encode(this.value(), { ecc: 'M', border: 2 });
    let path = '';
    result.data.forEach((row, y) =>
      row.forEach((dark, x) => {
        if (dark) path += `M${x} ${y}h1v1h-1z`;
      }),
    );
    return { size: result.size, path };
  });
}
