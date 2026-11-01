import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** A quantity must be more than nothing. Empty is left to `Validators.required`. */
export const greaterThanZero: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value: unknown = control.value;
  return typeof value === 'number' && value <= 0
    ? { message: 'Enter a quantity greater than zero.' }
    : null;
};

/** A business date ("2026-12-03") may not come after the given day (today). */
export function dateNotAfter(limit: string, message: string): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value: unknown = control.value;
    return typeof value === 'string' && value !== '' && value > limit ? { message } : null;
  };
}

/** A business date may not come before the given day (the purchase date). */
export function dateNotBefore(limit: string, message: string): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value: unknown = control.value;
    return typeof value === 'string' && value !== '' && value < limit ? { message } : null;
  };
}

/**
 * Text that is only spaces counts as empty. `Validators.required` lets "   " through, and the server trims before it checks, so
 * pair this with it: the error is the same `required` one, so the field shows its usual "Enter ..." sentence.
 */
export const notBlank: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value: unknown = control.value;
  return typeof value === 'string' && value.length > 0 && value.trim() === ''
    ? { required: true }
    : null;
};
