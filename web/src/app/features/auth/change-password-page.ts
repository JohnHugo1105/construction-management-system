import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { startWith } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { safeReturnUrl } from '../../core/session/auth-guards';
import { SessionState } from '../../core/session/session-state';
import { applyServerErrors, injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Alert } from '../../shared/ui/alert/alert';
import { Button } from '../../shared/ui/button/button';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { PasswordHints } from './password-hints';
import { passwordPolicyValidator } from './password-policy';

function matchesNewPassword(control: AbstractControl): ValidationErrors | null {
  const wanted = control.parent?.get('newPassword')?.value;
  return control.value === wanted
    ? null
    : { message: "The two passwords don't match. Type the new password again." };
}

/**
 * Choose a new password. Reached from the user menu, or forced after an administrator set a temporary password; while it is
 * forced the shell shows nothing else, so this page is the only way forward.
 */
@Component({
  selector: 'app-change-password-page',
  imports: [
    ReactiveFormsModule,
    PageHeader,
    Alert,
    ApiErrorAlert,
    FormField,
    Control,
    Button,
    PasswordHints,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './change-password-page.html',
})
export class ChangePasswordPage {
  protected readonly session = inject(SessionState);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly toast = inject(ToastService);

  protected readonly forced = this.session.mustChangePassword();

  protected readonly form = new FormGroup({
    currentPassword: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    newPassword: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, passwordPolicyValidator(() => this.session.username())],
    }),
    confirmPassword: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, matchesNewPassword],
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
  protected readonly extraMessages = signal<readonly string[]>([]);

  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  constructor() {
    // The confirmation is checked against the new password, so check it again whenever the new password changes.
    this.form.controls.newPassword.valueChanges
      .pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(() =>
        this.form.controls.confirmPassword.updateValueAndValidity({ emitEvent: false }),
      );
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
    this.extraMessages.set([]);
    const { currentPassword, newPassword } = this.form.getRawValue();
    try {
      await this.session.changePassword({ currentPassword, newPassword });
      this.toast.success('Your password has been changed.');
      await this.router.navigateByUrl(
        safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl')),
      );
    } catch (failure) {
      const problem = asApiError(failure);
      this.error.set(problem);
      this.extraMessages.set(applyServerErrors(this.form, problem));
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }
}
