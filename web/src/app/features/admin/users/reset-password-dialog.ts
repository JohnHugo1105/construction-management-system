import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom, startWith } from 'rxjs';
import { ApiError, asApiError } from '../../../core/api/api-error';
import { applyServerErrors, injectFocusFirstInvalid } from '../../../shared/forms/server-errors';
import { ApiErrorAlert } from '../../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../../shared/ui/button/button';
import { Dialog } from '../../../shared/ui/dialog/dialog';
import { Control } from '../../../shared/ui/form-field/control';
import { FormField } from '../../../shared/ui/form-field/form-field';
import { PasswordHints } from '../../auth/password-hints';
import { passwordPolicyValidator } from '../../auth/password-policy';
import { UsersApi } from './users-api';
import { UserAccount } from './users.models';

/** An administrator sets a temporary password for someone. The person must choose their own the next time they sign in. */
@Component({
  selector: 'app-reset-password-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, PasswordHints, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Reset password" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>
          Set a temporary password for {{ user().displayName }}. Tell them what it is; they will be
          asked to choose their own the next time they sign in.
        </p>

        @if (error(); as problem) {
          <app-api-error-alert [error]="problem" />
        }

        <form
          id="reset-password-form"
          class="form"
          [formGroup]="form"
          (ngSubmit)="submit()"
          #formElement
        >
          <div class="stack stack--tight">
            <app-form-field
              label="Temporary password"
              [control]="form.controls.newPassword"
              requiredMessage="Enter a temporary password."
            >
              <input
                appControl
                type="password"
                formControlName="newPassword"
                autocomplete="new-password"
              />
            </app-form-field>
            <app-password-hints
              [password]="typed().newPassword ?? ''"
              [username]="user().username"
            />
          </div>
        </form>
      </div>

      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          Cancel
        </button>
        <button
          appButton
          variant="primary"
          type="submit"
          form="reset-password-form"
          [busy]="saving()"
        >
          Set temporary password
        </button>
      </div>
    </app-dialog>
  `,
})
export class ResetPasswordDialog {
  readonly user = input.required<UserAccount>();

  /** The password was set. */
  readonly done = output<void>();
  readonly dismissed = output<void>();

  private readonly api = inject(UsersApi);
  private readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  protected readonly form = new FormGroup({
    newPassword: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, passwordPolicyValidator(() => this.user().username)],
    }),
  });

  protected readonly typed = toSignal(
    this.form.valueChanges.pipe(startWith(this.form.getRawValue())),
    {
      initialValue: this.form.getRawValue(),
    },
  );

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);

  protected cancel(): void {
    this.dialog().dismiss();
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(
        this.api.resetPassword(this.user().id, this.form.getRawValue().newPassword),
      );
      this.done.emit();
    } catch (failure) {
      const problem = asApiError(failure);
      const unmatched = applyServerErrors(this.form, problem);
      if (!problem.isValidation || unmatched.length > 0) this.error.set(problem);
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }
}
