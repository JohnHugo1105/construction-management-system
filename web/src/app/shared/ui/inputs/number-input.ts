import { ChangeDetectionStrategy, Component, forwardRef, input } from '@angular/core';
import { NG_VALIDATORS, NG_VALUE_ACCESSOR } from '@angular/forms';
import { formatScaledDecimal, parseScaledDecimal } from '../../presentation/scaled-decimal';
import { Control } from '../form-field/control';
import { ScaledInput } from './scaled-input';

/**
 * A plain decimal number such as a quantity or a count of hours. `decimals` is how many places are allowed (2 by default;
 * quantities use up to 4). The form value is a number or null. Add `unit` to show a fixed word after the box ("hours").
 * Limits go on the control as usual: `Validators.min(1)`, `Validators.max(24)`.
 */
@Component({
  selector: 'app-number-input',
  imports: [Control],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => NumberInput), multi: true },
    { provide: NG_VALIDATORS, useExisting: forwardRef(() => NumberInput), multi: true },
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
      @if (unit()) {
        <span class="input-group__affix input-group__affix--end">{{ unit() }}</span>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
    }
  `,
})
export class NumberInput extends ScaledInput {
  readonly decimals = input(2);
  readonly unit = input('');
  readonly allowNegative = input(false);

  protected override parse(text: string): number | null {
    const scaled = parseScaledDecimal(text, this.decimals(), {
      allowNegative: this.allowNegative(),
    });
    return scaled === null ? null : scaled / 10 ** this.decimals();
  }

  protected override format(value: number): string {
    return formatScaledDecimal(Math.round(value * 10 ** this.decimals()), this.decimals(), {
      group: false,
      trimZeros: true,
    });
  }

  protected override invalidMessage(): string {
    return this.decimals() === 0
      ? 'Enter a whole number.'
      : `Enter a number with at most ${this.decimals()} decimal places.`;
  }
}
