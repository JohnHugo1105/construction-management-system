import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

export interface SegmentOption<T extends string> {
  readonly value: T;
  readonly label: string;
}

/**
 * A short row of exclusive choices that switch what a panel shows ("By division | By sub-project | By type"). Each segment is a
 * real button that says whether it is the one showing (`aria-pressed`), at least 44px tall, and the row wraps rather than
 * squeezing the words.
 */
@Component({
  selector: 'app-segmented-control',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="segments" role="group" [attr.aria-label]="label()">
      @for (option of options(); track option.value) {
        <button
          type="button"
          class="segment"
          [attr.aria-pressed]="option.value === value()"
          (click)="selected.emit(option.value)"
        >
          {{ option.label }}
        </button>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .segments {
      display: inline-flex;
      flex-wrap: wrap;
      max-width: 100%;
      border: 1px solid var(--color-border-strong);
      border-radius: var(--radius-md);
      background: var(--color-surface);
      overflow: clip;
    }

    .segment {
      min-height: var(--touch-target);
      padding: 0 var(--space-4);
      border: 0;
      border-right: 1px solid var(--color-border);
      background: transparent;
      color: var(--color-text);
      font-weight: 600;
      cursor: pointer;
    }

    .segment:last-child {
      border-right: 0;
    }

    .segment:hover {
      background: var(--color-surface-2);
    }

    .segment[aria-pressed='true'] {
      background: var(--color-brand-soft);
      color: var(--color-brand-text);
    }

    .segment:focus-visible {
      outline-offset: -3px;
    }
  `,
})
export class SegmentedControl<T extends string> {
  readonly label = input.required<string>();
  readonly options = input.required<readonly SegmentOption<T>[]>();
  readonly value = input.required<T>();

  readonly selected = output<T>();
}
