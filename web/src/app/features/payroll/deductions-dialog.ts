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
import {
  FormArray,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { SessionState } from '../../core/session/session-state';
import { injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { formatMoney } from '../../shared/presentation/money';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { Icon } from '../../shared/ui/icon/icon';
import { MoneyInput } from '../../shared/ui/inputs/money-input';
import { PayrollApi } from './payroll-api';
import { DeductionInput, PayrollLine, PayrollRun } from './payroll.models';

type DeductionForm = FormGroup<{
  description: FormControl<string>;
  amount: FormControl<number | null>;
}>;

/**
 * Enters one worker's deductions on a draft payroll: what is taken from their pay (SSS, PhilHealth, Pag-IBIG, tax, an advance).
 * A person types them, because the system doesn't calculate statutory amounts. The API refuses deductions above the pay and
 * says so; its message is shown above the rows. Saving replaces the worker's deductions.
 */
@Component({
  selector: 'app-deductions-dialog',
  imports: [
    ReactiveFormsModule,
    Dialog,
    Button,
    FormField,
    Control,
    MoneyInput,
    Icon,
    Alert,
    ApiErrorAlert,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Edit deductions" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>
          <strong>{{ line().workerName }}</strong> earned
          <span class="money">{{ gross() }}</span> in this payroll. Deductions can't be more than
          that.
        </p>
        <p class="text-muted text-small">
          SSS, PhilHealth, Pag-IBIG and tax aren't calculated for you. Enter the amounts that apply.
        </p>

        @if (general().length > 0) {
          <app-alert tone="danger" title="The deductions couldn't be saved">
            @for (message of general(); track message) {
              <span class="general">{{ message }}</span>
            }
          </app-alert>
        }
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [allowRetry]="problem.status === 409"
            reloadLabel="Reload payroll"
            (reload)="reloaded.emit()"
          />
        }

        <form
          id="deductions-form"
          class="form"
          [formGroup]="form"
          (ngSubmit)="submit()"
          #formElement
        >
          @if (rows.length === 0) {
            <p class="text-muted">
              No deductions. Add one if something is taken from this worker's pay.
            </p>
          }
          <div formArrayName="rows" class="rows">
            @for (row of rows.controls; track row; let index = $index) {
              <div class="row" [formGroupName]="index">
                <app-form-field
                  [label]="'Deduction ' + (index + 1)"
                  hint="For example SSS contribution."
                  [control]="row.controls.description"
                  requiredMessage="Describe the deduction, for example SSS contribution."
                >
                  <input
                    appControl
                    formControlName="description"
                    autocomplete="off"
                    maxlength="120"
                  />
                </app-form-field>
                <app-form-field
                  [label]="'Amount ' + (index + 1)"
                  [control]="row.controls.amount"
                  requiredMessage="Enter the amount."
                >
                  <app-money-input formControlName="amount" placeholder="0.00" />
                </app-form-field>
                <button
                  appButton
                  variant="ghost"
                  type="button"
                  class="row__remove"
                  (click)="remove(index)"
                >
                  <app-icon name="close" [size]="18" />
                  Remove deduction {{ index + 1 }}
                </button>
              </div>
            }
          </div>
          <div>
            <button appButton type="button" (click)="add()">
              <app-icon name="plus" [size]="18" />
              Add a deduction
            </button>
          </div>
        </form>
      </div>

      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          Cancel
        </button>
        <button appButton variant="primary" type="submit" form="deductions-form" [busy]="saving()">
          Save deductions
        </button>
      </div>
    </app-dialog>
  `,
  styles: `
    .general {
      display: block;
    }

    .rows {
      display: grid;
      gap: var(--space-4);
    }

    .row {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      gap: var(--space-3);
      padding: var(--space-3);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
    }

    .row__remove {
      justify-self: start;
    }
  `,
})
export class DeductionsDialog implements OnInit {
  readonly run = input.required<PayrollRun>();
  readonly line = input.required<PayrollLine>();

  /** The run the API returned with the worker's new deductions. */
  readonly saved = output<PayrollRun>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(PayrollApi);
  private readonly session = inject(SessionState);
  private readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  protected readonly form = new FormGroup({ rows: new FormArray<DeductionForm>([]) });

  protected readonly gross = computed(() =>
    formatMoney(this.line().grossPayMinor, this.session.currencyCode()),
  );

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly general = signal<readonly string[]>([]);

  protected get rows(): FormArray<DeductionForm> {
    return this.form.controls.rows;
  }

  ngOnInit(): void {
    for (const item of this.line().deductions)
      this.rows.push(this.newRow(item.description, item.amountMinor));
  }

  protected add(): void {
    this.rows.push(this.newRow('', null));
  }

  protected remove(index: number): void {
    this.rows.removeAt(index);
  }

  protected cancel(): void {
    this.dialog().dismiss();
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    // A row left completely blank is simply dropped; a half-filled one is a mistake the person should see.
    for (let index = this.rows.length - 1; index >= 0; index--) {
      const row = this.rows.at(index).getRawValue();
      if (row.description.trim() === '' && row.amount === null) this.rows.removeAt(index);
    }
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }

    const deductions: DeductionInput[] = this.rows.getRawValue().map((row) => ({
      description: row.description.trim(),
      amountMinor: row.amount ?? 0,
    }));
    this.saving.set(true);
    this.error.set(null);
    this.general.set([]);
    try {
      const run = this.run();
      const updated = await firstValueFrom(
        this.api.setDeductions(run.id, this.line().workerId, deductions, run.version),
      );
      this.saved.emit(updated);
    } catch (failure) {
      const problem = asApiError(failure);
      // The API reports every deduction problem under one key, "deductions": the message names the worker and the rule.
      const messages = problem.errors['deductions'] ?? [];
      const others = Object.entries(problem.errors)
        .filter(([key]) => key !== 'deductions')
        .flatMap(([, list]) => list);
      this.general.set([...messages, ...others]);
      if (!problem.isValidation || (messages.length === 0 && others.length === 0))
        this.error.set(problem);
    } finally {
      this.saving.set(false);
    }
  }

  private newRow(description: string, amountMinor: number | null): DeductionForm {
    return new FormGroup({
      description: new FormControl(description, {
        nonNullable: true,
        validators: [Validators.required, Validators.maxLength(120)],
      }),
      amount: new FormControl<number | null>(amountMinor, {
        validators: [
          Validators.required,
          (control): ValidationErrors | null =>
            control.value !== null && control.value <= 0
              ? { message: 'Enter an amount greater than zero.' }
              : null,
        ],
      }),
    });
  }
}
