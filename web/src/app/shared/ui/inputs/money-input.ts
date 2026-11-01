import {
  ChangeDetectionStrategy,
  Component,
  computed,
  forwardRef,
  inject,
  input,
} from '@angular/core';
import { NG_VALIDATORS, NG_VALUE_ACCESSOR } from '@angular/forms';
import { SessionState } from '../../../core/session/session-state';
import { currencySymbol, formatMoneyInput, parseMoneyInput } from '../../presentation/money';
import { Control } from '../form-field/control';
import { ScaledInput } from './scaled-input';

/**
 * An amount of money. The form value is whole minor units (centavos) or null; the person types "1,250.50". The currency
 * symbol comes from the session. Use it inside a FormField:
 *
 *     <app-form-field label="Contract price" [control]="form.controls.contractPrice">
 *       <app-money-input formControlName="contractPrice" />
 *     </app-form-field>
 */
@Component({
  selector: 'app-money-input',
  imports: [Control],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => MoneyInput), multi: true },
    { provide: NG_VALIDATORS, useExisting: forwardRef(() => MoneyInput), multi: true },
  ],
  template: `
    <div class="input-group">
      <span class="input-group__affix input-group__affix--start" aria-hidden="true">{{
        symbol()
      }}</span>
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
    </div>
  `,
  styles: `
    :host {
      display: block;
    }
  `,
})
export class MoneyInput extends ScaledInput {
  private readonly session = inject(SessionState);

  readonly allowNegative = input(false);

  protected readonly symbol = computed(() => currencySymbol(this.session.currencyCode()));

  protected override parse(text: string): number | null {
    return parseMoneyInput(text, { allowNegative: this.allowNegative() });
  }

  protected override format(value: number): string {
    return formatMoneyInput(value);
  }

  protected override invalidMessage(text: string): string {
    return /^\D*\d[\d,]*\.\d{3,}$/.test(text.trim())
      ? 'Use at most two decimal places, like 1,250.50.'
      : 'Enter an amount like 1,250.50.';
  }
}
