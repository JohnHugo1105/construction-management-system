import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Code128, QrCode } from './badge-symbols';
import { Badge } from './timeclock.models';

/** "ABCD-EFGH-JKMN": the code in groups of four, for a person to read and type. */
export function groupedCode(code: string): string {
  return code.length === 12 ? `${code.slice(0, 4)}-${code.slice(4, 8)}-${code.slice(8)}` : code;
}

/**
 * One badge, card-sized (85.6 x 54 mm when printed): the company, the worker's name, employee number and job title, and the code as a Code 128
 * barcode (what most scanners read), a QR code and in letters. Nothing about pay.
 */
@Component({
  selector: 'app-badge-card',
  imports: [Code128, QrCode],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article class="badge-card" [attr.aria-label]="'Badge of ' + badge().fullName">
      <p class="badge-card__company">{{ company() }}</p>
      <div class="badge-card__top">
        <div class="badge-card__who">
          <p class="badge-card__name">{{ badge().fullName }}</p>
          <p class="badge-card__about">{{ about() }}</p>
        </div>
        @if (badge().code; as code) {
          <app-qr-code class="badge-card__qr" [value]="code" />
        }
      </div>
      @if (badge().code; as code) {
        <app-code128 class="badge-card__barcode" [value]="code" />
        <p class="badge-card__code">{{ grouped() }}</p>
      }
    </article>
  `,
  styles: `
    :host {
      display: block;
    }

    .badge-card {
      display: grid;
      grid-template-rows: auto 1fr auto auto;
      gap: 1.2mm;
      width: 85.6mm;
      height: 54mm;
      padding: 3mm 4mm;
      overflow: hidden;
      border: 0.3mm solid #7a8696;
      border-radius: 3mm;
      background: #ffffff;
      color: #000000;
      font-size: 2.6mm;
      line-height: 1.25;
    }

    .badge-card__company {
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .badge-card__top {
      display: grid;
      grid-template-columns: 1fr 19mm;
      gap: 2mm;
      min-height: 0;
    }

    .badge-card__name {
      font-size: 4mm;
      font-weight: 700;
      overflow-wrap: anywhere;
    }

    .badge-card__qr {
      width: 19mm;
      height: 19mm;
    }

    .badge-card__barcode {
      height: 9mm;
    }

    .badge-card__code {
      font-family: ui-monospace, monospace;
      font-size: 3mm;
      font-weight: 700;
      letter-spacing: 0.08em;
      text-align: center;
    }

    @media screen and (max-width: 400px) {
      .badge-card {
        width: 100%;
        height: auto;
        aspect-ratio: 85.6 / 54;
      }
    }
  `,
})
export class BadgeCard {
  readonly badge = input.required<Badge>();
  readonly company = input.required<string>();

  protected readonly about = computed(() =>
    [this.badge().employeeNumber, this.badge().jobTitle].filter(Boolean).join(' · '),
  );
  protected readonly grouped = computed(() => groupedCode(this.badge().code ?? ''));
}
