import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnInit,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../../core/api/api-error';
import { ToastService } from '../../../core/notifications/toast-service';
import { SessionState } from '../../../core/session/session-state';
import { DEFAULT_TIME_ZONE } from '../../../shared/presentation/business-time';
import { applyServerErrors, injectFocusFirstInvalid } from '../../../shared/forms/server-errors';
import { numberBetween } from '../../../shared/forms/validators';
import { Alert } from '../../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../../shared/ui/button/button';
import { Control } from '../../../shared/ui/form-field/control';
import { FieldGroup } from '../../../shared/ui/form-field/field-group';
import { FormField } from '../../../shared/ui/form-field/form-field';
import { NumberInput } from '../../../shared/ui/inputs/number-input';
import { PercentInput } from '../../../shared/ui/inputs/percent-input';
import { PageHeader } from '../../../shared/ui/page-header/page-header';
import { Skeleton } from '../../../shared/ui/skeleton/skeleton';
import { OrganizationApi } from './organization-api';
import { Organization, VarianceBasis } from './organization.models';

/** The API names these fields differently from the form, which shows percentages instead of basis points. */
const SERVER_FIELD_NAMES = {
  vatRateBps: 'vatRate',
  overtimeMultiplierBps: 'overtimeRate',
} as const;

function knownTimeZones(): readonly string[] {
  try {
    const zones = Intl.supportedValuesOf('timeZone');
    return zones.includes('UTC') ? zones : ['UTC', ...zones];
  } catch {
    return [DEFAULT_TIME_ZONE, 'UTC'];
  }
}

/**
 * The company's settings: name, currency, VAT rate, time zone, paid hours, overtime rate, and whether budgets are compared with
 * or without VAT. Rates are typed as percentages and sent as basis points. Saving refreshes the session so the new currency and
 * time zone apply at once.
 */
@Component({
  selector: 'app-company-settings-page',
  imports: [
    ReactiveFormsModule,
    PageHeader,
    FormField,
    FieldGroup,
    Control,
    PercentInput,
    NumberInput,
    Button,
    Alert,
    ApiErrorAlert,
    Skeleton,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './company-settings-page.html',
})
export class CompanySettingsPage implements OnInit {
  private readonly api = inject(OrganizationApi);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);

  protected readonly timeZones = knownTimeZones();

  protected readonly form = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(120)],
    }),
    currencyCode: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(/^[A-Za-z]{3}$/)],
    }),
    timeZoneId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    vatRate: new FormControl<number | null>(null, {
      validators: [
        Validators.required,
        numberBetween(0, 10_000, 'Enter a VAT rate between 0% and 100%.'),
      ],
    }),
    hoursPerDay: new FormControl<number | null>(null, {
      validators: [
        Validators.required,
        numberBetween(1, 24, 'Enter paid hours per day between 1 and 24.'),
      ],
    }),
    overtimeRate: new FormControl<number | null>(null, {
      validators: [
        Validators.required,
        numberBetween(10_000, 30_000, 'Enter an overtime rate between 100% and 300%.'),
      ],
    }),
    payrollRunsPerMonth: new FormControl<number | null>(null, {
      validators: [
        Validators.required,
        numberBetween(1, 4, 'Enter how many pay runs you make in a month, from 1 to 4.'),
      ],
    }),
    varianceBasis: new FormControl<VarianceBasis | null>(null, {
      validators: [Validators.required],
    }),
  });

  protected readonly loaded = signal<Organization | null>(null);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extraMessages = signal<readonly string[]>([]);
  protected readonly showSummary = signal(false);

  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  ngOnInit(): void {
    void this.load();
  }

  protected reload(): void {
    void this.load();
  }

  protected async submit(): Promise<void> {
    const current = this.loaded();
    if (this.saving() || !current) return;
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
    try {
      const saved = await firstValueFrom(
        this.api.update({
          name: value.name.trim(),
          currencyCode: value.currencyCode.trim().toUpperCase(),
          vatRateBps: value.vatRate ?? 0,
          timeZoneId: value.timeZoneId.trim(),
          hoursPerDay: value.hoursPerDay ?? 0,
          overtimeMultiplierBps: value.overtimeRate ?? 0,
          varianceBasis: value.varianceBasis ?? 'IncludingVat',
          payrollRunsPerMonth: value.payrollRunsPerMonth ?? current.payrollRunsPerMonth,
          version: current.version,
        }),
      );
      this.show(saved);
      await this.session.refresh();
      this.toast.success('Company settings saved.');
    } catch (failure) {
      const problem = asApiError(failure);
      this.error.set(problem);
      this.extraMessages.set(applyServerErrors(this.form, problem, SERVER_FIELD_NAMES));
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.show(await firstValueFrom(this.api.get()));
    } catch (failure) {
      this.error.set(asApiError(failure));
    } finally {
      this.loading.set(false);
    }
  }

  private show(organization: Organization): void {
    this.loaded.set(organization);
    this.form.reset({
      name: organization.name,
      currencyCode: organization.currencyCode,
      timeZoneId: organization.timeZoneId,
      vatRate: organization.vatRateBps,
      hoursPerDay: organization.hoursPerDay,
      overtimeRate: organization.overtimeMultiplierBps,
      payrollRunsPerMonth: organization.payrollRunsPerMonth,
      varianceBasis: organization.varianceBasis,
    });
  }
}
