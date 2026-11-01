import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnInit,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { SessionState } from '../../core/session/session-state';
import { injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { formatMoney } from '../../shared/presentation/money';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { MoneyInput } from '../../shared/ui/inputs/money-input';
import { describeFormFailure } from '../workforce/form-failure';
import { PayrollDeduction, PayrollLine, PayrollRun, StatutoryAgency } from './payroll.models';
import { StatutoryApi } from './statutory-api';
import { AGENCY_LABEL } from './statutory-rules';

/**
 * Changes one calculated contribution or tax amount on a draft payroll, with a reason (for example the agency's statement shows a
 * different figure). The reason stays on the payroll. Recalculating the payroll puts the table's amount back.
 */
@Component({
  selector: 'app-adjust-statutory-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, MoneyInput, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog [title]="'Adjust ' + label()" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>
          The table gave <span class="money">{{ calculated() }}</span> for
          <strong>{{ line().workerName }}</strong
          >. Enter the amount to take instead.
        </p>

        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            [allowRetry]="problem.status === 409"
            reloadLabel="Reload payroll"
            (reload)="reloaded.emit()"
          />
        }

        <form id="adjust-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <app-form-field
            label="Amount to take"
            hint="Enter 0 to take nothing."
            [control]="form.controls.amount"
            requiredMessage="Enter the amount."
          >
            <app-money-input formControlName="amount" placeholder="0.00" />
          </app-form-field>
          <app-form-field
            label="Why is it changed?"
            [control]="form.controls.reason"
            requiredMessage="Say why the amount is changed."
          >
            <textarea appControl formControlName="reason" rows="2" maxlength="300"></textarea>
          </app-form-field>
        </form>
      </div>

      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          Cancel
        </button>
        <button appButton variant="primary" type="submit" form="adjust-form" [busy]="saving()">
          Save amount
        </button>
      </div>
    </app-dialog>
  `,
})
export class AdjustStatutoryDialog implements OnInit {
  readonly run = input.required<PayrollRun>();
  readonly line = input.required<PayrollLine>();
  readonly deduction = input.required<PayrollDeduction>();

  readonly saved = output<PayrollRun>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(StatutoryApi);
  private readonly session = inject(SessionState);
  private readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  protected readonly form = new FormGroup({
    amount: new FormControl<number | null>(null, { validators: [Validators.required] }),
    reason: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(300)],
    }),
  });

  protected readonly label = computed(() => {
    const agency = this.deduction().agency;
    if (!agency) return 'deduction';
    return agency === 'WithholdingTax' ? 'withholding tax' : `${AGENCY_LABEL[agency]} contribution`;
  });
  protected readonly calculated = computed(() =>
    formatMoney(
      this.deduction().calculatedMinor ?? this.deduction().amountMinor,
      this.session.currencyCode(),
    ),
  );

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);

  ngOnInit(): void {
    this.form.reset({
      amount: this.deduction().amountMinor,
      reason: this.deduction().adjustmentReason ?? '',
    });
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
    this.saving.set(true);
    this.error.set(null);
    this.extra.set([]);
    try {
      const run = this.run();
      const updated = await firstValueFrom(
        this.api.adjust(
          run.id,
          this.line().workerId,
          this.deduction().agency as StatutoryAgency,
          value.amount ?? 0,
          value.reason.trim(),
          run.version,
        ),
      );
      this.saved.emit(updated);
    } catch (failure) {
      const outcome = describeFormFailure(this.form, asApiError(failure), {
        amountMinor: 'amount',
      });
      this.error.set(outcome.alert);
      this.extra.set(outcome.extra);
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }
}
