import { Directive, input, signal } from '@angular/core';
import { AbstractControl, ControlValueAccessor, ValidationErrors, Validator } from '@angular/forms';

/**
 * The shared behaviour of the money, percent and number inputs: the person types what they would write ("1,250.50"), the form
 * holds a number (or null), and what is shown is tidied when they leave the field. Text that is not a valid value gives the
 * control a `message` error, which the surrounding FormField shows once the field was touched.
 */
@Directive()
export abstract class ScaledInput implements ControlValueAccessor, Validator {
  readonly placeholder = input('');

  protected readonly text = signal('');
  protected readonly disabled = signal(false);

  private notifyChange: (value: number | null) => void = () => undefined;
  private notifyTouched: () => void = () => undefined;

  /** The number the text stands for, or null when it is not a valid value. */
  protected abstract parse(text: string): number | null;

  /** The text to show for a stored number. */
  protected abstract format(value: number): string;

  /** What to tell the person when the text is not a valid value. */
  protected abstract invalidMessage(text: string): string;

  writeValue(value: number | null | undefined): void {
    this.text.set(value === null || value === undefined ? '' : this.format(value));
  }

  registerOnChange(fn: (value: number | null) => void): void {
    this.notifyChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.notifyTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }

  validate(_control: AbstractControl): ValidationErrors | null {
    const text = this.text().trim();
    if (text === '' || this.parse(text) !== null) return null;
    return { message: this.invalidMessage(text) };
  }

  protected handleInput(event: Event): void {
    const text = (event.target as HTMLInputElement).value;
    this.text.set(text);
    this.notifyChange(text.trim() === '' ? null : this.parse(text));
  }

  protected handleBlur(): void {
    const parsed = this.text().trim() === '' ? null : this.parse(this.text());
    if (parsed !== null) this.text.set(this.format(parsed));
    this.notifyTouched();
  }
}
