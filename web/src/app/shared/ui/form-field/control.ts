import { Directive, inject } from '@angular/core';
import { FormField } from './form-field';

/**
 * Marks the input, select or textarea inside a FormField: gives it the shared look and ties it to the field's label, hint
 * and error (id, aria-describedby, aria-invalid, aria-required). Outside a FormField it only applies the look.
 */
@Directive({
  selector: 'input[appControl], select[appControl], textarea[appControl]',
  host: {
    class: 'input',
    '[attr.id]': 'field?.id ?? null',
    '[attr.aria-invalid]': "field?.invalid() ? 'true' : null",
    '[attr.aria-describedby]': 'field?.describedBy() ?? null',
    '[attr.aria-required]': "field?.required() ? 'true' : null",
  },
})
export class Control {
  protected readonly field = inject(FormField, { optional: true });
}
