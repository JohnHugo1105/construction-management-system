import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { SessionState } from '../../core/session/session-state';
import { applyServerErrors, injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { todayIn } from '../../shared/presentation/business-time';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { WorkforceApi } from './workforce-api';
import { WorkerDetail } from './workforce.models';

/**
 * Edits a worker's personal details (`/workers/:id/edit`). The employee number, hire date and pay are not here: they are not
 * personal details, and pay changes go through "Change pay rate" so every change stays on record. A save carries the version the
 * person opened; if someone else changed the worker meanwhile they are told and can reload.
 */
@Component({
  selector: 'app-worker-profile-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    PageHeader,
    FormField,
    Control,
    Button,
    Alert,
    ApiErrorAlert,
    Skeleton,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header
      title="Edit profile"
      backLabel="Back to worker"
      [backLink]="['/workers', id()]"
    />

    @if (loading() && !loaded()) {
      <app-skeleton [lines]="5" label="Loading worker" />
    } @else if (!loaded()) {
      <app-api-error-alert
        [error]="error()"
        [allowRetry]="true"
        reloadLabel="Try again"
        (reload)="reload()"
      />
    } @else {
      <div class="card stack stack--loose form-card">
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extraMessages()"
            reloadLabel="Reload worker"
            (reload)="reload()"
          />
        } @else if (showSummary()) {
          <app-alert tone="danger" title="Some details need your attention"
            >Check the highlighted fields and try again.</app-alert
          >
        }

        <form class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <div class="form__section">
            <h2 class="card__title">Name</h2>
            <div class="form__row">
              <app-form-field
                label="First name"
                [control]="form.controls.firstName"
                requiredMessage="Enter the first name."
              >
                <input appControl formControlName="firstName" autocomplete="off" />
              </app-form-field>
              <app-form-field
                label="Last name"
                [control]="form.controls.lastName"
                requiredMessage="Enter the last name."
              >
                <input appControl formControlName="lastName" autocomplete="off" />
              </app-form-field>
            </div>
            <app-form-field
              label="Middle name"
              [optional]="true"
              [control]="form.controls.middleName"
            >
              <input appControl formControlName="middleName" autocomplete="off" />
            </app-form-field>
          </div>

          <div class="form__section">
            <h2 class="card__title">About them</h2>
            <div class="form__row">
              <app-form-field
                label="Date of birth"
                [optional]="true"
                [control]="form.controls.dateOfBirth"
              >
                <input appControl type="date" formControlName="dateOfBirth" [max]="today" />
              </app-form-field>
              <app-form-field
                label="Contact number"
                [optional]="true"
                [control]="form.controls.contactNumber"
              >
                <input appControl type="tel" formControlName="contactNumber" autocomplete="off" />
              </app-form-field>
            </div>
            <div class="form__row">
              <app-form-field
                label="Marital status"
                [optional]="true"
                [control]="form.controls.maritalStatus"
              >
                <input appControl formControlName="maritalStatus" autocomplete="off" />
              </app-form-field>
              <app-form-field
                label="Spouse's name"
                [optional]="true"
                [control]="form.controls.spouseName"
              >
                <input appControl formControlName="spouseName" autocomplete="off" />
              </app-form-field>
            </div>
            <div class="form__row">
              <app-form-field label="Religion" [optional]="true" [control]="form.controls.religion">
                <input appControl formControlName="religion" autocomplete="off" />
              </app-form-field>
              <app-form-field
                label="Education"
                [optional]="true"
                [control]="form.controls.educationalAttainment"
              >
                <input appControl formControlName="educationalAttainment" autocomplete="off" />
              </app-form-field>
            </div>
          </div>

          <div class="form__section">
            <h2 class="card__title">Addresses and reference</h2>
            <app-form-field
              label="Present address"
              [optional]="true"
              [control]="form.controls.presentAddress"
            >
              <textarea
                appControl
                formControlName="presentAddress"
                rows="2"
                maxlength="250"
              ></textarea>
            </app-form-field>
            <app-form-field
              label="Provincial address"
              [optional]="true"
              [control]="form.controls.provincialAddress"
            >
              <textarea
                appControl
                formControlName="provincialAddress"
                rows="2"
                maxlength="250"
              ></textarea>
            </app-form-field>
            <app-form-field
              label="Reference"
              [optional]="true"
              hint="Someone who can vouch for the worker."
              [control]="form.controls.reference"
            >
              <input appControl formControlName="reference" autocomplete="off" />
            </app-form-field>
          </div>

          <div class="form__actions form__actions--sticky">
            <a appButton [routerLink]="['/workers', id()]">Cancel</a>
            <button appButton variant="primary" type="submit" [busy]="saving()">
              Save profile
            </button>
          </div>
        </form>
      </div>
    }
  `,
})
export class WorkerProfilePage {
  /** Bound from the route (`/workers/:id/edit`). */
  readonly id = input.required<string>();

  private readonly api = inject(WorkforceApi);
  private readonly session = inject(SessionState);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly today = todayIn(this.session.timeZoneId());

  protected readonly form = new FormGroup({
    firstName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(80)],
    }),
    middleName: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(80)] }),
    lastName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(80)],
    }),
    dateOfBirth: new FormControl('', { nonNullable: true }),
    maritalStatus: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(40)],
    }),
    spouseName: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(120)] }),
    religion: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(60)] }),
    educationalAttainment: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(80)],
    }),
    presentAddress: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(250)],
    }),
    provincialAddress: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(250)],
    }),
    contactNumber: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(40)],
    }),
    reference: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(200)] }),
  });

  protected readonly loaded = signal<WorkerDetail | null>(null);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extraMessages = signal<readonly string[]>([]);
  protected readonly showSummary = signal(false);

  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => void this.load(id));
    });
  }

  /** Fetches the worker again and replaces what is in the form (on first load, and after a conflict). */
  protected async load(id: string): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const worker = await firstValueFrom(this.api.get(id));
      this.loaded.set(worker);
      this.form.reset({
        firstName: worker.firstName,
        middleName: worker.middleName ?? '',
        lastName: worker.lastName,
        dateOfBirth: worker.dateOfBirth ?? '',
        maritalStatus: worker.maritalStatus ?? '',
        spouseName: worker.spouseName ?? '',
        religion: worker.religion ?? '',
        educationalAttainment: worker.educationalAttainment ?? '',
        presentAddress: worker.presentAddress ?? '',
        provincialAddress: worker.provincialAddress ?? '',
        contactNumber: worker.contactNumber ?? '',
        reference: worker.reference ?? '',
      });
    } catch (failure) {
      this.error.set(asApiError(failure));
    } finally {
      this.loading.set(false);
    }
  }

  protected reload(): void {
    void this.load(this.id());
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.showSummary.set(true);
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }

    const value = this.form.getRawValue();
    const text = (entry: string): string | null => entry.trim() || null;
    this.saving.set(true);
    this.showSummary.set(false);
    this.error.set(null);
    this.extraMessages.set([]);
    try {
      await firstValueFrom(
        this.api.updateProfile(this.id(), {
          firstName: value.firstName.trim(),
          middleName: text(value.middleName),
          lastName: value.lastName.trim(),
          dateOfBirth: value.dateOfBirth || null,
          maritalStatus: text(value.maritalStatus),
          spouseName: text(value.spouseName),
          religion: text(value.religion),
          educationalAttainment: text(value.educationalAttainment),
          presentAddress: text(value.presentAddress),
          provincialAddress: text(value.provincialAddress),
          contactNumber: text(value.contactNumber),
          reference: text(value.reference),
          version: this.loaded()?.version ?? 0,
        }),
      );
      this.toast.success('Profile saved.');
      await this.router.navigate(['/workers', this.id()]);
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
