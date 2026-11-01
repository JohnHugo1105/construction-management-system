import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiError, asApiError } from '../../core/api/api-error';
import { safeReturnUrl } from '../../core/session/auth-guards';
import { SessionState } from '../../core/session/session-state';
import { injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { AuthFrame } from './auth-frame';

/**
 * Sign-in. A wrong password or a locked account is shown above the form in the API's own words: it never says which of the two
 * fields was wrong, and no field is marked. The setup link appears only while the system has no users.
 */
@Component({
  selector: 'app-sign-in-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    AuthFrame,
    Alert,
    ApiErrorAlert,
    FormField,
    Control,
    Button,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './sign-in-page.html',
})
export class SignInPage {
  private readonly session = inject(SessionState);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly form = new FormGroup({
    username: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  protected readonly signingIn = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly needsSetup = signal(false);
  protected readonly sessionEnded = this.route.snapshot.queryParamMap.get('reason') === 'expired';

  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();
  private readonly passwordInput = viewChild<ElementRef<HTMLInputElement>>('passwordInput');

  constructor() {
    void this.prepare();
  }

  protected async submit(): Promise<void> {
    if (this.signingIn()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }

    this.signingIn.set(true);
    this.error.set(null);
    const { username, password } = this.form.getRawValue();
    try {
      await this.session.signIn({ username: username.trim(), password });
      await this.router.navigateByUrl(
        safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl')),
      );
    } catch (failure) {
      this.error.set(asApiError(failure));
      this.form.controls.password.reset('');
      this.passwordInput()?.nativeElement.focus();
    } finally {
      this.signingIn.set(false);
    }
  }

  /** A fresh anti-forgery cookie (an old one from an ended session would be refused), and whether setup is still open. */
  private async prepare(): Promise<void> {
    await this.session.ensureCsrf();
    this.needsSetup.set(await this.session.needsSetup());
  }
}
