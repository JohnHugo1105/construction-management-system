import { AbstractControl } from '@angular/forms';
import { ApiError } from '../../core/api/api-error';
import { applyServerErrors } from '../../shared/forms/server-errors';

/** What a form shows above its fields after the API refused a save. */
export interface FormFailure {
  /** The problem to show in an alert, or null when the field messages say everything. */
  readonly alert: ApiError | null;
  /** Messages for fields this form has not got, shown inside the alert. */
  readonly extra: readonly string[];
}

/**
 * Puts the API's per-field messages under their fields and decides what is left to say above the form. A refusal that is
 * only about fields needs no alert (the messages sit under the fields); a conflict, a business rule, a server failure, or a
 * message for a field the form does not have does.
 */
export function describeFormFailure(
  form: AbstractControl,
  problem: ApiError,
  aliases: Readonly<Record<string, string>> = {},
): FormFailure {
  const extra = applyServerErrors(form, problem, aliases);
  const fieldsOnly = problem.isValidation && extra.length === 0;
  return { alert: fieldsOnly ? null : problem, extra };
}
