import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnInit,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { applyServerErrors, injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { formatDate } from '../../shared/presentation/business-time';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FieldGroup } from '../../shared/ui/form-field/field-group';
import { FormField } from '../../shared/ui/form-field/form-field';
import { PayRulesApi } from './pay-rules-api';
import { CompanyHoliday, HolidayType } from './payroll.models';
import { HOLIDAY_TYPE_LABEL } from './premium-rules';

/**
 * Adds a holiday (date, name, type), or changes the name and type of one (`holiday` set: its date stays). The server refuses a second
 * holiday on a date and a change to a day an approved payroll covers; its messages show under the fields or above them.
 */
@Component({
  selector: 'app-holiday-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, FieldGroup, Control, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog
      [title]="holiday() ? 'Change holiday' : 'Add holiday'"
      [locked]="saving()"
      (closed)="dismissed.emit()"
    >
      <form class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extraMessages()"
            [allowRetry]="problem.status === 409"
            reloadLabel="Reload holidays"
            (reload)="reloaded.emit()"
          />
        }
        @if (holiday(); as current) {
          <p>
            <strong>{{ dateLabel(current.date) }}</strong>
          </p>
        } @else {
          <app-form-field
            label="Date"
            [control]="form.controls.date"
            requiredMessage="Choose the date of the holiday."
          >
            <input appControl type="date" formControlName="date" />
          </app-form-field>
        }
        <app-form-field
          label="Name"
          [control]="form.controls.name"
          requiredMessage="Enter the name of the holiday."
        >
          <input appControl formControlName="name" maxlength="80" autocomplete="off" />
        </app-form-field>
        <app-field-group
          label="Kind of day"
          [control]="form.controls.type"
          requiredMessage="Choose regular holiday or special day."
        >
          <div class="choice-list">
            @for (choice of types; track choice.value) {
              <label class="choice">
                <input type="radio" formControlName="type" [value]="choice.value" />
                <span class="choice__text">
                  <span class="choice__label">{{ choice.label }}</span>
                  <span class="choice__hint">{{ choice.hint }}</span>
                </span>
              </label>
            }
          </div>
        </app-field-group>
      </form>
      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          Cancel
        </button>
        <button appButton variant="primary" type="button" [busy]="saving()" (click)="submit()">
          {{ holiday() ? 'Save holiday' : 'Add holiday' }}
        </button>
      </div>
    </app-dialog>
  `,
})
export class HolidayDialog implements OnInit {
  /** The holiday being changed; null to add one. */
  readonly holiday = input<CompanyHoliday | null>(null);

  readonly saved = output<CompanyHoliday>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(PayRulesApi);
  private readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  protected readonly types: readonly { value: HolidayType; label: string; hint: string }[] = [
    { value: 'Regular', label: HOLIDAY_TYPE_LABEL.Regular, hint: 'Paid even when not worked.' },
    { value: 'Special', label: HOLIDAY_TYPE_LABEL.Special, hint: 'Paid only when worked.' },
  ];

  protected readonly form = new FormGroup({
    date: new FormControl('', { nonNullable: true }),
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(80)],
    }),
    type: new FormControl<HolidayType | ''>('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extraMessages = signal<readonly string[]>([]);

  ngOnInit(): void {
    const current = this.holiday();
    if (current) this.form.patchValue({ name: current.name, type: current.type });
    else this.form.controls.date.addValidators(Validators.required);
  }

  protected dateLabel(date: string): string {
    return formatDate(date);
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
    const { date, name, type } = this.form.getRawValue();
    const current = this.holiday();
    this.saving.set(true);
    this.error.set(null);
    this.extraMessages.set([]);
    try {
      this.saved.emit(
        await firstValueFrom(
          current
            ? this.api.changeHoliday(current.id, name.trim(), type as HolidayType, current.version)
            : this.api.addHoliday(date, name.trim(), type as HolidayType),
        ),
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
