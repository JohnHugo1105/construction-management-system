import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { startWith } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { HOME_URL } from '../../core/session/auth-guards';
import { SessionState } from '../../core/session/session-state';
import { ToastService } from '../../core/notifications/toast-service';
import { applyServerErrors, injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { AuthFrame } from './auth-frame';
import { PasswordHints } from './password-hints';
import { passwordPolicyValidator } from './password-policy';

/** First-time setup: creates the company and its first administrator, then signs that person in. Open only while there are no users. */
@Component({
  selector: 'app-setup-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    AuthFrame,
    ApiErrorAlert,
    FormField,
    Control,
    Button,
    PasswordHints,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './setup-page.html',
})
export class SetupPage {
  private readonly session = inject(SessionState);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  private readonly username = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/^[A-Za-z0-9._@-]{3,64}$/)],
  });

  protected readonly form = new FormGroup({
    companyName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(120)],
    }),
    displayName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(120)],
    }),
    username: this.username,
    password: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, passwordPolicyValidator(() => this.username.value)],
    }),
  });

  // The hints follow what is typed, whichever field changes.
  protected readonly typed = toSignal(
    this.form.valueChanges.pipe(startWith(this.form.getRawValue())),
    {
      initialValue: this.form.getRawValue(),
    },
  );

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extraMessages = signal<readonly string[]>([]);

  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.controls.password.updateValueAndValidity({ emitEvent: false }); // the username may have changed since
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.extraMessages.set([]);
    const value = this.form.getRawValue();
    try {
      await this.session.bootstrap({
        companyName: value.companyName.trim(),
        displayName: value.displayName.trim(),
        username: value.username.trim(),
        password: value.password,
      });
      this.toast.success('Your company is set up. Welcome.');
      await this.router.navigateByUrl(HOME_URL);
    } catch (failure) {
      const problem = asApiError(failure);
      this.error.set(problem);
      this.extraMessages.set(applyServerErrors(this.form, problem, { name: 'companyName' }));
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }
}
