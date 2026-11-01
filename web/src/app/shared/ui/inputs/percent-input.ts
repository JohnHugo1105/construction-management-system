import { ChangeDetectionStrategy, Component, forwardRef } from '@angular/core';
import { NG_VALIDATORS, NG_VALUE_ACCESSOR } from '@angular/forms';
import { formatBpsInput, parsePercentInput } from '../../presentation/percent';
import { Control } from '../form-field/control';
import { ScaledInput } from './scaled-input';

/**
 * A percentage. The form value is basis points (1,200 is 12%) or null; the person types "12" or "12.5". Use it inside a
 * FormField, like the money input.
 */
@Component({
  selector: 'app-percent-input',
  imports: [Control],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => PercentInput), multi: true },
    { provide: NG_VALIDATORS, useExisting: forwardRef(() => PercentInput), multi: true },
  ],
  template: `
    <div class="input-group">
      <input
        appControl
        type="text"
        inputmode="decimal"
        autocomplete="off"
        [value]="text()"
        [disabled]="disabled()"
        [attr.placeholder]="placeholder() || null"
        (input)="handleInput($event)"
        (blur)="handleBlur()"
      />
      <span class="input-group__affix input-group__affix--end" aria-hidden="true">%</span>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }
  `,
})
export class PercentInput extends ScaledInput {
  protected override parse(text: string): number | null {
    return parsePercentInput(text);
  }

  protected override format(value: number): string {
    return formatBpsInput(value);
  }

  protected override invalidMessage(text: string): string {
    return /^\d+\.\d{3,}/.test(text.trim())
      ? 'Use at most two decimal places, like 12.5.'
      : 'Enter a percentage like 12 or 12.5.';
  }
}
