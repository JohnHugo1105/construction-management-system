import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { applyServerErrors, injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { todayIn } from '../../shared/presentation/business-time';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Control } from '../../shared/ui/form-field/control';
import { FieldGroup } from '../../shared/ui/form-field/field-group';
import { FormField } from '../../shared/ui/form-field/form-field';
import { MoneyInput } from '../../shared/ui/inputs/money-input';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { newRequestId } from './request-id';
import { WorkforceApi } from './workforce-api';
import { NO_REST_DAY, REST_DAY_CHOICES, REST_DAY_HINT, RestDayChoice } from './worker-rest-day';
import { DEFAULT_STATUTORY, STATUTORY_CHOICES, statutoryWords } from './worker-statutory';
import { RateUnit } from './workforce.models';

/** The API names the pay rate `rateMinor`; the form calls it the pay rate. */
const SERVER_FIELD_NAMES = { rateMinor: 'rate' } as const;

/**
 * Hires a worker (`/workers/new`). Pay is confidential and the API refuses a hire without a rate, so the form asks for one
 * only from people who may see pay; everyone else is told plainly that a payroll user has to do it. The form carries one
 * request id for the whole visit, so pressing Save twice hires one person.
 */
@Component({
  selector: 'app-worker-form-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    PageHeader,
    FormField,
    FieldGroup,
    Control,
    MoneyInput,
    Button,
    Alert,
    ApiErrorAlert,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Hire a worker" backLabel="All workers" [backLink]="['/workers']" />

    @if (!canSeePay()) {
      <app-alert tone="warning" title="A payroll user needs to hire workers">
        Hiring needs a pay rate, and pay details are limited to payroll staff. Ask an HR officer or
        an accountant to hire this worker.
        <div alertActions>
          <a appButton routerLink="/workers">Back to workers</a>
        </div>
      </app-alert>
    } @else {
      <div class="card stack stack--loose form-card">
        @if (error(); as problem) {
          <app-api-error-alert [error]="problem" [extra]="extraMessages()" />
        } @else if (showSummary()) {
          <app-alert tone="danger" title="Some details need your attention"
            >Check the highlighted fields and try again.</app-alert
          >
        }

        <form class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <div class="form__section">
            <h2 class="card__title">Who they are</h2>
            <app-form-field
              label="Employee number"
              hint="Each worker needs a number that no one else has."
              [control]="form.controls.employeeNumber"
              requiredMessage="Enter the employee number."
            >
              <input appControl formControlName="employeeNumber" autocomplete="off" />
            </app-form-field>
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
            <h2 class="card__title">Job</h2>
            <div class="form__row">
              <app-form-field
                label="Hire date"
                [control]="form.controls.dateHired"
                requiredMessage="Choose the date the worker was hired."
              >
                <input appControl type="date" formControlName="dateHired" [max]="today" />
              </app-form-field>
              <app-form-field
                label="Designation"
                hint="For example Mason or Foreman."
                [control]="form.controls.designation"
                requiredMessage="Enter the designation, for example Mason."
              >
                <input appControl formControlName="designation" autocomplete="off" />
              </app-form-field>
            </div>
            <app-form-field label="Rest day" [hint]="restDayHint" [control]="form.controls.restDay">
              <select appControl formControlName="restDay">
                @for (choice of restDayChoices; track choice.value) {
                  <option [value]="choice.value">{{ choice.label }}</option>
                }
              </select>
            </app-form-field>
          </div>

          <div class="form__section">
            <h2 class="card__title">Pay</h2>
            <app-form-field
              label="Pay rate"
              [control]="form.controls.rate"
              requiredMessage="Enter the pay rate."
            >
              <app-money-input formControlName="rate" placeholder="0.00" />
            </app-form-field>
            <app-field-group
              label="Paid"
              [control]="form.controls.rateUnit"
              requiredMessage="Choose whether the rate is per day or per hour."
            >
              <div class="choice-list">
                <label class="choice">
                  <input type="radio" formControlName="rateUnit" value="Daily" />
                  <span class="choice__text">
                    <span class="choice__label">Per day</span>
                    <span class="choice__hint"
                      >A day's pay. The usual choice for construction crews.</span
                    >
                  </span>
                </label>
                <label class="choice">
                  <input type="radio" formControlName="rateUnit" value="Hourly" />
                  <span class="choice__text">
                    <span class="choice__label">Per hour</span>
                    <span class="choice__hint">Paid for each hour worked.</span>
                  </span>
                </label>
              </div>
            </app-field-group>

            <details class="contributions">
              <summary>Contributions: {{ contributionWords() }}</summary>
              <div class="choice-list">
                @for (choice of statutoryChoices; track choice.key) {
                  <label class="choice">
                    <input type="checkbox" [formControlName]="choice.key" />
                    <span class="choice__text">
                      <span class="choice__label">{{ choice.label }}</span>
                      @if (choice.hint) {
                        <span class="choice__hint">{{ choice.hint }}</span>
                      }
                    </span>
                  </label>
                }
              </div>
            </details>
          </div>

          <div class="form__actions form__actions--sticky">
            <a appButton routerLink="/workers">Cancel</a>
            <button appButton variant="primary" type="submit" [busy]="saving()">Hire worker</button>
          </div>
        </form>
      </div>
    }
  `,
  styles: `
    .contributions summary {
      display: flex;
      align-items: center;
      min-height: 44px;
      font-weight: 600;
      cursor: pointer;
    }

    .contributions .choice-list {
      margin-top: var(--space-2);
    }
  `,
})
export class WorkerFormPage {
  private readonly api = inject(WorkforceApi);
  private readonly session = inject(SessionState);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly canSeePay = this.session.permit(Permission.payrollRead);
  protected readonly today = todayIn(this.session.timeZoneId());

  protected readonly form = new FormGroup({
    employeeNumber: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(40)],
    }),
    firstName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(80)],
    }),
    middleName: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(80)] }),
    lastName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(80)],
    }),
    dateHired: new FormControl(this.today, {
      nonNullable: true,
      validators: [Validators.required],
    }),
    designation: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(80)],
    }),
    // No rest day unless HR sets one: a rest day earns a premium, so it is never assumed.
    restDay: new FormControl<RestDayChoice>(NO_REST_DAY, { nonNullable: true }),
    rate: new FormControl<number | null>(null, {
      validators: [Validators.required, Validators.min(1)],
    }),
    // Neither choice is pre-selected: a day's pay and an hour's pay differ eight times over.
    rateUnit: new FormControl<RateUnit | null>(null, { validators: [Validators.required] }),
    // A new worker pays every fund and is taxed; the choices are folded away until someone needs them.
    sssMember: new FormControl(DEFAULT_STATUTORY.sssMember, { nonNullable: true }),
    philHealthMember: new FormControl(DEFAULT_STATUTORY.philHealthMember, { nonNullable: true }),
    pagIbigMember: new FormControl(DEFAULT_STATUTORY.pagIbigMember, { nonNullable: true }),
    taxExempt: new FormControl(DEFAULT_STATUTORY.taxExempt, { nonNullable: true }),
  });

  protected readonly statutoryChoices = STATUTORY_CHOICES;
  protected readonly restDayChoices = REST_DAY_CHOICES;
  protected readonly restDayHint = REST_DAY_HINT;
  private readonly statutoryValue = toSignal(this.form.valueChanges, {
    initialValue: this.form.getRawValue(),
  });
  protected readonly contributionWords = computed(() => {
    const value = this.statutoryValue();
    return statutoryWords({
      sssMember: value.sssMember ?? true,
      philHealthMember: value.philHealthMember ?? true,
      pagIbigMember: value.pagIbigMember ?? true,
      taxExempt: value.taxExempt ?? false,
    });
  });

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extraMessages = signal<readonly string[]>([]);
  protected readonly showSummary = signal(false);

  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  /** One id for this visit to the form: a second press of Save hires the same person, not another. */
  private readonly requestId = newRequestId();

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.showSummary.set(true);
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }

    const value = this.form.getRawValue();
    this.saving.set(true);
    this.showSummary.set(false);
    this.error.set(null);
    this.extraMessages.set([]);
    try {
      const hired = await firstValueFrom(
        this.api.hire({
          employeeNumber: value.employeeNumber.trim(),
          firstName: value.firstName.trim(),
          middleName: value.middleName.trim() || null,
          lastName: value.lastName.trim(),
          dateHired: value.dateHired,
          designation: value.designation.trim(),
          rateMinor: value.rate ?? 0,
          rateUnit: value.rateUnit as RateUnit,
          clientRequestId: this.requestId,
          sssMember: value.sssMember,
          philHealthMember: value.philHealthMember,
          pagIbigMember: value.pagIbigMember,
          taxExempt: value.taxExempt,
          restDay: value.restDay,
        }),
      );
      this.toast.success('Worker hired.');
      await this.router.navigate(['/workers', hired.id]);
    } catch (failure) {
      const problem = asApiError(failure);
      this.error.set(problem);
      this.extraMessages.set(applyServerErrors(this.form, problem, SERVER_FIELD_NAMES));
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }
}
