import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * A number must lie within limits. The message is written for the person ("Enter a VAT rate between 0% and 100%."), so it
 * reads right even when the stored value is in other units (basis points).
 */
export function numberBetween(min: number, max: number, message: string): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value;
    if (value === null || value === undefined || value === '') return null;
    return typeof value === 'number' && value >= min && value <= max ? null : { message };
  };
}
