import { Injector, afterNextRender, inject } from '@angular/core';
import { AbstractControl } from '@angular/forms';

/** The part of an ApiError this file needs, so shared code does not depend on core. */
export interface FieldErrorSource {
  readonly errors: Readonly<Record<string, readonly string[]>>;
}

/**
 * Puts the API's per-field messages on the matching form controls and returns the messages that belong to no field on this
 * form, so the page can still show them. The API keys errors by camelCase request field name; `aliases` maps the ones the
 * form names differently (`{ contractPriceMinor: 'contractPrice' }`).
 *
 * A control's own validators run again when its value changes, which clears the server message by itself.
 */
export function applyServerErrors(
  form: AbstractControl,
  error: FieldErrorSource,
  aliases: Readonly<Record<string, string>> = {},
): string[] {
  const unmatched: string[] = [];
  for (const [field, messages] of Object.entries(error.errors)) {
    const control = form.get(aliases[field] ?? field);
    if (control) {
      control.setErrors({ server: messages.join(' ') });
      control.markAsTouched();
    } else {
      unmatched.push(...messages);
    }
  }
  return unmatched;
}

/** Puts one message on one control, as if the server had reported it (for example a business rule about a name). */
export function setServerError(control: AbstractControl, message: string): void {
  control.setErrors({ server: message });
  control.markAsTouched();
}

/**
 * Moves the keyboard to the first field that needs attention, so a failed submit never leaves a person guessing. It waits for
 * the next render, because the fields only become `aria-invalid` once Angular has drawn their error. Create it in a field
 * initializer and call it with the form element:
 *
 *     private readonly focusInvalid = injectFocusFirstInvalid();
 *     ...
 *     this.focusInvalid(this.formElement()?.nativeElement);
 */
export function injectFocusFirstInvalid(): (root: ParentNode | null | undefined) => void {
  const injector = inject(Injector);
  return (root) =>
    afterNextRender(() => root?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus(), {
      injector,
    });
}
