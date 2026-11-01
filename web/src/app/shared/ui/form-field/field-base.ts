import { Directive, computed, input } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, Validators } from '@angular/forms';
import { of, startWith, switchMap } from 'rxjs';
import { describeControlErrors } from '../../forms/validation-messages';

let nextFieldId = 0;

/**
 * What a labelled field needs, whether it wraps one input (FormField) or a group of choices (FieldGroup): a label, a hint
 * below, and one error line that appears once the person has touched the field. Pass `control` and the message is worked out
 * from its errors; pass `error` to show a message yourself.
 */
@Directive()
export abstract class FieldBase {
  readonly label = input.required<string>();
  readonly hint = input('');
  /** Marks the minority case: most fields are required, so only optional ones say so. */
  readonly optional = input(false);
  readonly control = input<AbstractControl | null>(null);
  readonly error = input<string | null>(null);
  /** The sentence shown when a required field is empty ("Enter the project name."). */
  readonly requiredMessage = input<string | undefined>(undefined);

  readonly id = `field-${++nextFieldId}`;
  readonly hintId = `${this.id}-hint`;
  readonly errorId = `${this.id}-error`;

  // Controls change without Angular knowing (touched, errors); their event stream lets OnPush views follow.
  private readonly controlEvents = toSignal(
    toObservable(this.control).pipe(
      switchMap((control) => (control ? control.events.pipe(startWith(null)) : of(null))),
    ),
    { initialValue: null },
  );

  readonly message = computed<string | null>(() => {
    this.controlEvents();
    const manual = this.error();
    if (manual) return manual;
    const control = this.control();
    if (!control || !control.invalid || !control.touched) return null;
    return describeControlErrors(control.errors, this.label(), this.requiredMessage());
  });

  readonly invalid = computed(() => this.message() !== null);

  readonly required = computed(() => {
    this.controlEvents();
    return this.control()?.hasValidator(Validators.required) ?? false;
  });

  readonly describedBy = computed(() => {
    const ids = [this.hint() ? this.hintId : null, this.message() ? this.errorId : null].filter(
      (id): id is string => id !== null,
    );
    return ids.length > 0 ? ids.join(' ') : null;
  });
}
