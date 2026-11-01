import { ChangeDetectionStrategy, Component, input, linkedSignal, output } from '@angular/core';
import { formatScaledDecimal, parseScaledDecimal } from '../../shared/presentation/scaled-decimal';

/** Hours as a number with at most two decimals, or null when the text is empty or not a number. */
export function parseHours(text: string): number | null {
  const scaled = parseScaledDecimal(text, 2);
  return scaled === null ? null : scaled / 100;
}

/** 8 shows as "8" and 7.5 as "7.5". */
export function formatHours(value: number | null): string {
  return value === null
    ? ''
    : formatScaledDecimal(Math.round(value * 100), 2, { group: false, trimZeros: true });
}

/**
 * A number of hours for a phone: big plus and minus buttons around a numeric box, so a crew's day can be filled in with a thumb.
 * It reports a number (or null for a blank or unreadable box) and never goes below zero or above `max`. The label says whose
 * hours these are, because there is one of these for every worker on the sheet.
 */
@Component({
  selector: 'app-hours-field',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="hours" [class.hours--invalid]="invalid()">
      <button
        type="button"
        class="hours__step"
        [attr.aria-label]="'Less: ' + label()"
        [disabled]="disabled()"
        (click)="step(-1)"
      >
        <span aria-hidden="true">&minus;</span>
      </button>
      <input
        class="hours__input"
        type="text"
        inputmode="decimal"
        autocomplete="off"
        [attr.aria-label]="label()"
        [attr.aria-invalid]="invalid() ? 'true' : null"
        [value]="text()"
        [disabled]="disabled()"
        (input)="typed($event)"
        (blur)="tidy()"
      />
      <button
        type="button"
        class="hours__step"
        [attr.aria-label]="'More: ' + label()"
        [disabled]="disabled()"
        (click)="step(1)"
      >
        <span aria-hidden="true">+</span>
      </button>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .hours {
      display: flex;
      align-items: stretch;
      min-width: 0;
    }

    .hours__step {
      flex: none;
      width: var(--touch-target);
      min-height: var(--touch-target);
      border: 1px solid var(--color-border-strong);
      background: var(--color-surface-2);
      color: var(--color-text);
      font-size: var(--text-xl);
      font-weight: 600;
      line-height: 1;
      cursor: pointer;
    }

    .hours__step:first-child {
      border-radius: var(--radius-md) 0 0 var(--radius-md);
      border-right: 0;
    }

    .hours__step:last-child {
      border-radius: 0 var(--radius-md) var(--radius-md) 0;
      border-left: 0;
    }

    .hours__step:hover:not(:disabled) {
      background: var(--color-border);
    }

    .hours__step:disabled {
      opacity: 0.55;
      cursor: not-allowed;
    }

    .hours__input {
      flex: 1 1 auto;
      width: 100%;
      min-width: 3.25rem;
      min-height: var(--touch-target);
      padding: 0 var(--space-2);
      border: 1px solid var(--color-border-strong);
      background: var(--color-surface);
      color: var(--color-text);
      font-size: var(--text-lg);
      font-variant-numeric: tabular-nums;
      text-align: center;
    }

    .hours__input:disabled {
      background: var(--color-surface-2);
      color: var(--color-text-muted);
    }

    .hours--invalid .hours__input {
      border-color: var(--color-danger);
      box-shadow: inset 0 0 0 1px var(--color-danger);
    }
  `,
})
export class HoursField {
  /** The hours, or null while the box is blank. */
  readonly value = input<number | null>(null);
  /** What these hours are and whose, for screen readers: "Regular hours for Juan Dela Cruz". */
  readonly label = input.required<string>();
  /** The most the box accepts (the plus button stops here). */
  readonly max = input(24);
  /** How much one tap of plus or minus changes the hours. */
  readonly stepBy = input(1);
  readonly disabled = input(false);
  /** Draw the box as having a problem. The message is shown by the page, next to the worker. */
  readonly invalid = input(false);

  readonly valueChange = output<number | null>();

  protected readonly text = linkedSignal(() => formatHours(this.value()));

  protected typed(event: Event): void {
    const text = (event.target as HTMLInputElement).value;
    this.text.set(text);
    this.valueChange.emit(parseHours(text));
  }

  /** Writes the box tidily when the person leaves it (7.50 becomes 7.5). */
  protected tidy(): void {
    const parsed = parseHours(this.text());
    if (parsed !== null) this.text.set(formatHours(parsed));
  }

  protected step(direction: 1 | -1): void {
    const current = this.value() ?? 0;
    const next = Math.min(
      this.max(),
      Math.max(0, Math.round((current + direction * this.stepBy()) * 100) / 100),
    );
    this.text.set(formatHours(next));
    this.valueChange.emit(next);
  }
}
