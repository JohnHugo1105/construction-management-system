import { ValidationErrors } from '@angular/forms';

/**
 * Turns a control's errors into one plain sentence that says what to do. Order matters: a message the server or a custom
 * validator wrote is more specific than "required", so it wins.
 */
export function describeControlErrors(
  errors: ValidationErrors | null,
  label: string,
  requiredMessage?: string,
): string | null {
  if (!errors) return null;
  const name = label.trim().toLowerCase();

  const server = errors['server'];
  if (typeof server === 'string' && server.length > 0) return server;
  const custom = errors['message'];
  if (typeof custom === 'string' && custom.length > 0) return custom;

  if (errors['required']) return requiredMessage ?? `Enter ${name}.`;

  const maxLength = readNumber(errors['maxlength'], 'requiredLength');
  if (maxLength !== null) return `${capitalize(name)} can be at most ${maxLength} characters.`;
  const minLength = readNumber(errors['minlength'], 'requiredLength');
  if (minLength !== null) return `${capitalize(name)} needs at least ${minLength} characters.`;
  if (errors['email']) return 'Enter an email address like name@company.com.';

  const min = readNumber(errors['min'], 'min');
  if (min !== null) return `Enter ${name} of ${min} or more.`;
  const max = readNumber(errors['max'], 'max');
  if (max !== null) return `Enter ${name} of ${max} or less.`;
  if (errors['pattern']) return `Check ${name} and try again.`;

  return `Check ${name} and try again.`;
}

function readNumber(value: unknown, key: string): number | null {
  if (value === null || typeof value !== 'object') return null;
  const found = (value as Record<string, unknown>)[key];
  return typeof found === 'number' ? found : null;
}

function capitalize(text: string): string {
  return text.length === 0 ? text : text[0]!.toUpperCase() + text.slice(1);
}
