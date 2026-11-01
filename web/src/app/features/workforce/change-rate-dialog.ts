import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  OnInit,
  inject,
  input,
  output,
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
import { firstValueFrom, startWith } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { SessionState } from '../../core/session/session-state';
import { injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { formatDate, todayIn } from '../../shared/presentation/business-time';
import { formatMoney } from '../../shared/presentation/money';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FieldGroup } from '../../shared/ui/form-field/field-group';
import { FormField } from '../../shared/ui/form-field/form-field';
import { MoneyInput } from '../../shared/ui/inputs/money-input';
import { describeFormFailure } from './form-failure';
import {
  categoryChoice,
  designationProblem,
  designationRule,
  latestRate,
  rateChangeDates,
  rateInForce,
} from './rate-change';
import { RATE_CATEGORIES, RATE_UNIT_LABEL } from './worker-status';
import { WorkforceApi } from './workforce-api';
import { RateCategory, RateUnit, WorkerDetail } from './workforce.models';

/** The API names the pay rate `rateMinor`; the form calls it the new rate. */
const SERVER_FIELD_NAMES = { rateMinor: 'rate' } as const;

/**
 * Adds a line to a worker's pay history (it never edits an old one). A promotion or demotion must change the designation and a
 * rate adjustment must keep it, so the designation field follows the category chosen. The new rate must start after the one
 * before it. Only shown to people who may see and change pay.
 */
@Component({
  selector: 'app-change-rate-dialog',
  imports: [
    ReactiveFormsModule,
    Dialog,
    Button,
    FieldGroup,
    FormField,
    Control,
    MoneyInput,
    ApiErrorAlert,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Change pay rate" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>
          {{ worker().fullName }} is now paid {{ nowLine() }}. The new rate is added to their pay
          history; earlier pay is never rewritten.
        </p>

        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            reloadLabel="Reload worker"
            (reload)="reloaded.emit()"
          />
        }

        <form id="rate-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <app-field-group
            label="What kind of change is it?"
            [control]="form.controls.category"
            requiredMessage="Choose what kind of change this is."
          >
            <div class="choice-list">
              @for (choice of categories; track choice.value) {
                <label class="choice">
                  <input type="radio" formControlName="category" [value]="choice.value" />
                  <span class="choice__text">
                    <span class="choice__label">{{ choice.label }}</span>
                    <span class="choice__hint">{{ choice.help }}</span>
                  </span>
                </label>
              }
            </div>
          </app-field-group>

          <app-form-field
            label="Designation"
            [hint]="designationHint()"
            [control]="form.controls.designation"
            requiredMessage="Enter the designation, for example Foreman."
          >
            <input appControl formControlName="designation" autocomplete="off" />
          </app-form-field>

          <app-form-field
            label="New pay rate"
            [control]="form.controls.rate"
            requiredMessage="Enter the new pay rate."
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
                <span class="choice__text"><span class="choice__label">Per day</span></span>
              </label>
              <label class="choice">
                <input type="radio" formControlName="rateUnit" value="Hourly" />
                <span class="choice__text"><span class="choice__label">Per hour</span></span>
              </label>
            </div>
          </app-field-group>

          <app-form-field
            label="Starts on"
            [hint]="dateHint()"
            [control]="form.controls.effectiveFrom"
            requiredMessage="Choose the date the new rate starts."
          >
            <input
              appControl
              type="date"
              formControlName="effectiveFrom"
              [min]="dates().earliest"
            />
          </app-form-field>

          <app-form-field label="Reason" [optional]="true" [control]="form.controls.reason">
            <textarea appControl formControlName="reason" rows="2" maxlength="300"></textarea>
          </app-form-field>
        </form>
      </div>

      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          Cancel
        </button>
        <button appButton variant="primary" type="submit" form="rate-form" [busy]="saving()">
          Save pay rate
        </button>
      </div>
    </app-dialog>
  `,
})
export class ChangeRateDialog implements OnInit {
  readonly worker = input.required<WorkerDetail>();

  /** The worker the API returned with the new line of pay history. */
  readonly changed = output<WorkerDetail>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(WorkforceApi);
  private readonly session = inject(SessionState);
  private readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  protected readonly categories = RATE_CATEGORIES;
  private readonly today = todayIn(this.session.timeZoneId());

  /** The designation the newest line of pay history holds: the one a promotion must change and an adjustment must keep. */
  private readonly baseline = computed(
    () => latestRate(this.worker().rates)?.designation ?? this.worker().designation ?? '',
  );

  protected readonly dates = computed(() =>
    rateChangeDates(this.worker().rates, this.worker().dateHired, this.today),
  );

  protected readonly form = new FormGroup({
    // Nothing is chosen for the person: the category decides what the designation must do.
    category: new FormControl<Exclude<RateCategory, 'Initial'> | null>(null, {
      validators: [Validators.required],
    }),
    designation: new FormControl('', {
      nonNullable: true,
      validators: [
        Validators.required,
        Validators.maxLength(80),
        (control: AbstractControl): ValidationErrors | null => this.designationCheck(control),
      ],
    }),
    rate: new FormControl<number | null>(null, {
      validators: [Validators.required, Validators.min(1)],
    }),
    rateUnit: new FormControl<RateUnit | null>(null, { validators: [Validators.required] }),
    effectiveFrom: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    reason: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(300)] }),
  });

  private readonly category = toSignal(
    this.form.controls.category.valueChanges.pipe(startWith(this.form.controls.category.value)),
    { initialValue: null },
  );

  protected readonly nowLine = computed(() => {
    const worker = this.worker();
    const rate = rateInForce(worker.rates, this.today);
    if (!rate) return 'at their current rate';
    return `${formatMoney(rate.rateMinor, this.session.currencyCode())} ${RATE_UNIT_LABEL[rate.rateUnit]} as ${rate.designation}`;
  });

  protected readonly designationHint = computed(() => {
    switch (designationRule(this.category())) {
      case 'keep':
        return 'A rate adjustment keeps the designation, so it stays the same.';
      case 'change':
        return `Enter the new designation. It is now ${this.baseline()}.`;
      default:
        return 'Choose the kind of change first.';
    }
  });

  protected readonly dateHint = computed(() => {
    const newest = latestRate(this.worker().rates);
    return newest
      ? `After ${formatDate(newest.effectiveFrom)}, when the current rate started. Work on earlier days keeps its old rate.`
      : '';
  });

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);

  constructor() {
    // The category decides the designation: keep it for an adjustment, ask for a new one for a promotion or demotion.
    this.form.controls.category.valueChanges
      .pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe((category) => {
        const designation = this.form.controls.designation;
        if (designationRule(category) === 'keep') {
          designation.setValue(this.baseline());
          designation.disable({ emitEvent: false });
        } else {
          if (designation.disabled) designation.enable({ emitEvent: false });
          designation.setValue('');
        }
        designation.markAsUntouched();
        designation.updateValueAndValidity({ emitEvent: false });
      });
  }

  ngOnInit(): void {
    // The worker is known only once the inputs are set: start from the next date a change may begin and the unit they are paid in.
    this.form.controls.effectiveFrom.setValue(this.dates().suggested);
    this.form.controls.rateUnit.setValue(this.worker().currentRateUnit);
  }

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

    const value = this.form.getRawValue();
    const category = categoryChoice(value.category)?.value;
    if (!category || value.rate === null || value.rateUnit === null) return;

    this.saving.set(true);
    this.error.set(null);
    this.extra.set([]);
    try {
      const updated = await firstValueFrom(
        this.api.changeRate(this.worker().id, {
          category,
          designation: value.designation.trim(),
          rateMinor: value.rate,
          rateUnit: value.rateUnit,
          effectiveFrom: value.effectiveFrom,
          reason: value.reason.trim() || null,
          version: this.worker().version,
        }),
      );
      this.changed.emit(updated);
    } catch (failure) {
      const outcome = describeFormFailure(this.form, asApiError(failure), SERVER_FIELD_NAMES);
      this.error.set(outcome.alert);
      this.extra.set(outcome.extra);
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }

  private designationCheck(control: AbstractControl): ValidationErrors | null {
    const category = control.parent?.get('category')?.value as RateCategory | null | undefined;
    // Nothing to compare until a category is chosen (and the worker is not known while the form is being built).
    if (!category) return null;
    const message = designationProblem(category, String(control.value ?? ''), this.baseline());
    return message ? { message } : null;
  }
}
