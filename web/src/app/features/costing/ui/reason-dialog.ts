import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable, firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../../core/api/api-error';
import { applyServerErrors, injectFocusFirstInvalid } from '../../../shared/forms/server-errors';
import { ApiErrorAlert } from '../../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../../shared/ui/button/button';
import { Dialog } from '../../../shared/ui/dialog/dialog';
import { Control } from '../../../shared/ui/form-field/control';
import { FormField } from '../../../shared/ui/form-field/form-field';

/**
 * Asks why a record is being taken out of the totals (a budget line removed, a cost voided) and does it. The page says what
 * will happen in `message`, in the pending tense ("Void this cost? ..."), and hands in what to run; this dialog keeps itself
 * open and the reason typed if the server refuses, and reports a conflict with a way to reload.
 */
@Component({
  selector: 'app-reason-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog [title]="title()" [locked]="busy()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>{{ message() }}</p>

        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            reloadLabel="Reload"
            (reload)="reloadRequested.emit()"
          />
        }

        <form id="reason-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <app-form-field
            [label]="reasonLabel()"
            [control]="form.controls.reason"
            [requiredMessage]="requiredMessage()"
          >
            <textarea appControl formControlName="reason" rows="3" maxlength="300"></textarea>
          </app-form-field>
        </form>
      </div>

      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="busy() ? '' : null" (click)="cancel()">
          Cancel
        </button>
        <button appButton variant="danger" type="submit" form="reason-form" [busy]="busy()">
          {{ confirmLabel() }}
        </button>
      </div>
    </app-dialog>
  `,
})
export class ReasonDialog {
  readonly title = input.required<string>();
  /** What will happen, said as a question's answer: "It stays on record, no longer counts in totals, and can't be restored." */
  readonly message = input.required<string>();
  readonly reasonLabel = input('Reason');
  readonly requiredMessage = input('Say why.');
  /** Names the result: "Void cost", "Remove budget line". */
  readonly confirmLabel = input.required<string>();
  /** Runs the change with the typed reason. */
  readonly perform = input.required<(reason: string) => Observable<unknown>>();

  /** The change went through. */
  readonly done = output<void>();
  /** The dialog was dismissed without changing anything. */
  readonly dismissed = output<void>();
  /** The record changed under the person: the page should refetch it. */
  readonly reloadRequested = output<void>();

  private readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  protected readonly form = new FormGroup({
    reason: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(300)],
    }),
  });

  protected readonly busy = signal(false);
  protected readonly error = signal<ApiError | null>(null);

  protected cancel(): void {
    this.dialog().dismiss();
  }

  protected async submit(): Promise<void> {
    if (this.busy()) return;
    // A reason of only spaces counts as empty; what is sent is trimmed.
    const reason = this.form.controls.reason.value.trim();
    this.form.markAllAsTouched();
    if (reason === '') this.form.controls.reason.setErrors({ required: true });
    if (this.form.invalid) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(this.perform()(reason));
      this.done.emit();
    } catch (failure) {
      const problem = asApiError(failure);
      const unmatched = applyServerErrors(this.form, problem);
      if (
        problem.isConflict ||
        problem.isRuleViolation ||
        problem.isServerFailure ||
        unmatched.length > 0 ||
        !problem.isValidation
      )
        this.error.set(problem);
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.busy.set(false);
    }
  }
}
