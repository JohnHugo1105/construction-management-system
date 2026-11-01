import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnInit,
  computed,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom, startWith } from 'rxjs';
import { ApiError, asApiError } from '../../../core/api/api-error';
import { ToastService } from '../../../core/notifications/toast-service';
import { ROLES } from '../../../core/session/roles';
import { applyServerErrors, injectFocusFirstInvalid } from '../../../shared/forms/server-errors';
import { Alert } from '../../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../../shared/ui/button/button';
import { Control } from '../../../shared/ui/form-field/control';
import { FieldGroup } from '../../../shared/ui/form-field/field-group';
import { FormField } from '../../../shared/ui/form-field/form-field';
import { PageHeader } from '../../../shared/ui/page-header/page-header';
import { Skeleton } from '../../../shared/ui/skeleton/skeleton';
import { PasswordHints } from '../../auth/password-hints';
import { passwordPolicyValidator } from '../../auth/password-policy';
import { UsersApi } from './users-api';
import { UserAccount } from './users.models';

/**
 * Adds a person (`/admin/users/new`) or changes one (`/admin/users/:id/edit`). A new person gets a temporary password and must
 * choose their own at first sign-in. The username is fixed once the account exists.
 */
@Component({
  selector: 'app-user-form-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    PageHeader,
    FormField,
    FieldGroup,
    Control,
    Button,
    Alert,
    ApiErrorAlert,
    Skeleton,
    PasswordHints,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './user-form-page.html',
})
export class UserFormPage implements OnInit {
  /** Bound from the route: set when editing, absent when adding. */
  readonly id = input<string | undefined>(undefined);

  private readonly api = inject(UsersApi);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly roles = ROLES;

  private readonly username = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/^[A-Za-z0-9._@-]{3,64}$/)],
  });

  protected readonly form = new FormGroup({
    displayName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(120)],
    }),
    username: this.username,
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.email, Validators.maxLength(254)],
    }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, passwordPolicyValidator(() => this.username.value)],
    }),
    roles: new FormControl<string[]>([], { nonNullable: true, validators: [Validators.required] }),
  });

  protected readonly typed = toSignal(
    this.form.valueChanges.pipe(startWith(this.form.getRawValue())),
    {
      initialValue: this.form.getRawValue(),
    },
  );

  protected readonly isEdit = computed(() => this.id() !== undefined);
  protected readonly loaded = signal<UserAccount | null>(null);
  protected readonly loadingUser = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extraMessages = signal<readonly string[]>([]);
  protected readonly showSummary = signal(false);

  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  ngOnInit(): void {
    const id = this.id();
    if (!id) return;
    // An existing account keeps its username, and its password is changed with "Reset password" instead.
    this.form.controls.username.disable();
    this.form.controls.password.disable();
    void this.load(id);
  }

  protected selected(role: string): boolean {
    return (this.typed().roles ?? []).includes(role);
  }

  protected toggleRole(role: string, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    const current = this.form.controls.roles.value;
    this.form.controls.roles.setValue(
      checked ? [...current, role] : current.filter((name) => name !== role),
    );
    this.form.controls.roles.markAsTouched();
  }

  protected reload(): void {
    const id = this.id();
    if (id) void this.load(id);
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.controls.password.updateValueAndValidity({ emitEvent: false });
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.showSummary.set(true);
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }

    this.saving.set(true);
    this.showSummary.set(false);
    this.error.set(null);
    this.extraMessages.set([]);
    const value = this.form.getRawValue();
    const email = value.email.trim() || null;
    const id = this.id();
    try {
      if (id) {
        await firstValueFrom(
          this.api.update(id, {
            displayName: value.displayName.trim(),
            email,
            roles: value.roles,
            version: this.loaded()?.version ?? 0,
          }),
        );
        this.toast.success('User saved.');
      } else {
        await firstValueFrom(
          this.api.create({
            username: value.username.trim(),
            displayName: value.displayName.trim(),
            email,
            password: value.password,
            roles: value.roles,
          }),
        );
        this.toast.success(
          `${value.displayName.trim()} can now sign in with the temporary password.`,
        );
      }
      await this.router.navigateByUrl('/admin/users');
    } catch (failure) {
      const problem = asApiError(failure);
      this.error.set(problem);
      this.extraMessages.set(applyServerErrors(this.form, problem));
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }

  private async load(id: string): Promise<void> {
    this.loadingUser.set(true);
    this.error.set(null);
    try {
      const user = await firstValueFrom(this.api.get(id));
      this.loaded.set(user);
      this.form.reset({
        displayName: user.displayName,
        username: user.username,
        email: user.email ?? '',
        password: '',
        roles: [...user.roles],
      });
    } catch (failure) {
      this.error.set(asApiError(failure));
    } finally {
      this.loadingUser.set(false);
    }
  }
}
