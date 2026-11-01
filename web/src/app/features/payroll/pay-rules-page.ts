import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
} from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { applyServerErrors, injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { PayRulesApi } from './pay-rules-api';
import { PayRules, PayRulesFactors } from './payroll.models';
import { FACTOR_FIELDS, bpsFromPercent, checkFactor, percentText } from './premium-rules';

type FactorKey = keyof PayRulesFactors;

function factorValidator(field: (typeof FACTOR_FIELDS)[number]) {
  return (control: AbstractControl): ValidationErrors | null => {
    const message = checkFactor(field, String(control.value ?? ''));
    return message ? { message } : null;
  };
}

/**
 * The company's pay rules (`/payroll/rules`): what an hour on a rest day, a special day or a regular holiday earns, the extra on
 * overtime on those days, and the night differential, each as a share of the ordinary rate. People who keep the rules
 * (payrollrules.manage) edit them; everyone who reads payroll sees them. Ordinary overtime is the company settings' multiple and is
 * only shown here. A change applies to payrolls worked out or recalculated afterwards.
 */
@Component({
  selector: 'app-pay-rules-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    PageHeader,
    FormField,
    Control,
    Button,
    ApiErrorAlert,
    Skeleton,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Pay rules" backLabel="Payroll" [backLink]="['/payroll']" />

    @if (loadError(); as problem) {
      <app-api-error-alert
        [error]="problem"
        [allowRetry]="true"
        reloadLabel="Try again"
        (reload)="resource.reload()"
      />
    } @else if (rules(); as current) {
      <div class="card stack stack--loose form-card">
        <p>
          What an hour on a rest day, a holiday or at night is paid, as a share of the ordinary
          hourly rate. Changes apply to payrolls worked out or recalculated from now on.
        </p>
        @if (current.isDefault) {
          <p class="text-muted text-small">
            These are the usual rates. Check them against the current labor rules.
          </p>
        }

        @if (canManage()) {
          @if (error(); as problem) {
            <app-api-error-alert
              [error]="problem"
              [extra]="extraMessages()"
              [allowRetry]="problem.status === 409"
              reloadLabel="Reload the rules"
              (reload)="reload()"
            />
          }
          <form class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
            @for (field of fields; track field.key) {
              <app-form-field
                [label]="field.label + ' (%)'"
                [hint]="field.hint"
                [control]="form.controls[field.key]"
              >
                <input
                  appControl
                  inputmode="decimal"
                  autocomplete="off"
                  [formControlName]="field.key"
                />
              </app-form-field>
            }
            <p class="text-muted text-small">
              Overtime on an ordinary day: {{ ordinaryOvertime() }}, set in company settings.
            </p>
            <div class="form__actions form__actions--sticky">
              <a appButton routerLink="/payroll">Cancel</a>
              <button appButton variant="primary" type="submit" [busy]="saving()">
                Save pay rules
              </button>
            </div>
          </form>
        } @else {
          <dl class="rules">
            @for (row of readRows(); track row.label) {
              <div class="rules__row">
                <dt>{{ row.label }}</dt>
                <dd>{{ row.value }}</dd>
              </div>
            }
          </dl>
        }
      </div>
    } @else {
      <app-skeleton [lines]="6" label="Loading the pay rules" />
    }
  `,
  styles: `
    .rules {
      display: grid;
      gap: var(--space-2);
    }

    .rules__row {
      display: flex;
      justify-content: space-between;
      gap: var(--space-3);
    }

    .rules__row dt {
      color: var(--color-text-muted);
    }

    .rules__row dd {
      font-weight: 600;
    }
  `,
})
export class PayRulesPage {
  private readonly api = inject(PayRulesApi);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);

  protected readonly canManage = this.session.permit(Permission.payrollRulesManage);
  protected readonly fields = FACTOR_FIELDS;

  protected readonly resource = rxResource({ stream: () => this.api.rules() });
  private readonly saved = signal<PayRules | null>(null);
  protected readonly rules = computed(
    () => this.saved() ?? (this.resource.hasValue() ? this.resource.value() : null),
  );
  protected readonly loadError = computed(() => {
    const failure = this.resource.error();
    return failure ? asApiError(failure) : null;
  });

  protected readonly form = new FormGroup(
    Object.fromEntries(
      FACTOR_FIELDS.map((field) => [
        field.key,
        new FormControl('', { nonNullable: true, validators: [factorValidator(field)] }),
      ]),
    ) as Record<FactorKey, FormControl<string>>,
  );

  protected readonly ordinaryOvertime = computed(() => {
    const rules = this.rules();
    return rules ? percentText(rules.overtimeMultiplierBps) : '';
  });

  protected readonly readRows = computed(() => {
    const rules = this.rules();
    if (!rules) return [];
    return [
      ...FACTOR_FIELDS.map((field) => ({
        label: field.label,
        value: percentText(rules[field.key]),
      })),
      { label: 'Overtime on an ordinary day', value: percentText(rules.overtimeMultiplierBps) },
    ];
  });

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extraMessages = signal<readonly string[]>([]);

  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();
  private filledFrom: PayRules | null = null;

  constructor() {
    // Fill the form when the rules arrive (and after a save or a reload). The rules change only then, never while a person types.
    effect(() => {
      const rules = this.rules();
      if (rules) untracked(() => this.fill(rules));
    });
  }

  private fill(rules: PayRules): void {
    this.filledFrom = rules;
    for (const field of FACTOR_FIELDS)
      this.form.controls[field.key].setValue(String(rules[field.key] / 100));
    this.form.markAsPristine();
  }

  protected reload(): void {
    this.error.set(null);
    this.saved.set(null);
    this.resource.reload();
  }

  protected async submit(): Promise<void> {
    const current = this.filledFrom;
    if (this.saving() || !current) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }
    const values = this.form.getRawValue();
    const factors = Object.fromEntries(
      FACTOR_FIELDS.map((field) => [field.key, bpsFromPercent(values[field.key]) ?? 0]),
    ) as unknown as PayRulesFactors;
    this.saving.set(true);
    this.error.set(null);
    this.extraMessages.set([]);
    try {
      this.saved.set(await firstValueFrom(this.api.saveRules(factors, current.version)));
      this.toast.success('Pay rules saved.');
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
