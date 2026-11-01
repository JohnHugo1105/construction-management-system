import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** One rule of the password policy and whether the password meets it yet. */
export interface PasswordCheck {
  readonly id: 'length' | 'letter' | 'number' | 'username';
  readonly text: string;
  readonly met: boolean;
}

export const MINIMUM_PASSWORD_LENGTH = 10;

/**
 * The rules the API enforces, mirrored so a person sees them tick off as they type: 10 or more characters, a letter, a number,
 * and not their username. The API stays the judge; this only saves a round trip and the guessing.
 */
export function checkPassword(password: string, username = ''): PasswordCheck[] {
  const checks: PasswordCheck[] = [
    {
      id: 'length',
      text: `At least ${MINIMUM_PASSWORD_LENGTH} characters`,
      met: password.length >= MINIMUM_PASSWORD_LENGTH,
    },
    { id: 'letter', text: 'At least one letter', met: /\p{L}/u.test(password) },
    { id: 'number', text: 'At least one number', met: /\p{Nd}/u.test(password) },
  ];
  const name = username.trim();
  if (name.length > 0) {
    checks.push({
      id: 'username',
      text: "Doesn't include your username",
      met: !password.toLowerCase().includes(name.toLowerCase()),
    });
  }
  return checks;
}

export function passwordMeetsPolicy(password: string, username = ''): boolean {
  return checkPassword(password, username).every((check) => check.met);
}

/** A form validator for a password field; the hints list shows which rule is missing. */
export function passwordPolicyValidator(username: () => string = () => ''): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = String(control.value ?? '');
    if (value === '') return null;
    return passwordMeetsPolicy(value, username())
      ? null
      : { message: 'Make your password meet every rule below.' };
  };
}
