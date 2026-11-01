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
import { todayIn } from '../../shared/presentation/business-time';
import { formatMoney } from '../../shared/presentation/money';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FieldGroup } from '../../shared/ui/form-field/field-group';
import { FormField } from '../../shared/ui/form-field/form-field';
import { MoneyInput } from '../../shared/ui/inputs/money-input';
import { ClientRequestId } from '../costing/client-request-id';
import { describeFormFailure } from '../workforce/form-failure';
import { BillingApi } from './billing-api';
import { PAYMENT_METHODS, billName } from './billing-rules';
import { Bill, PaymentMethod } from './billing.models';

/**
 * Records money the client paid against an issued bill. It is a person saying the money was received: nothing is collected. The
 * amount starts as what is still to collect, which the server worked out; the server refuses more than that.
 */
@Component({
  selector: 'app-payment-dialog',
  imports: [
    ReactiveFormsModule,
    Dialog,
    Button,
    FormField,
    FieldGroup,
    Control,
    MoneyInput,
    ApiErrorAlert,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog [title]="title()" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            [allowRetry]="problem.status === 409"
            reloadLabel="Reload bill"
            (reload)="reloaded.emit()"
          />
        }
        <form id="payment-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <app-form-field
            label="Amount received"
            [hint]="amountHint()"
            [control]="form.controls.amount"
            requiredMessage="Enter the amount received."
          >
            <app-money-input formControlName="amount" data-autofocus />
          </app-form-field>
          <app-form-field
            label="Date received"
            [control]="form.controls.paidOn"
            requiredMessage="Choose the date the payment was received."
          >
            <input appControl type="date" formControlName="paidOn" [max]="today" />
          </app-form-field>
          <app-field-group
            label="How the client paid"
            [control]="form.controls.method"
            requiredMessage="Choose how the client paid."
          >
            <div class="choice-list choice-list--inline">
              @for (method of methods; track method.value) {
                <label class="choice">
                  <input type="radio" formControlName="method" [value]="method.value" />
                  <span class="choice__text"
                    ><span class="choice__label">{{ method.label }}</span></span
                  >
                </label>
              }
            </div>
          </app-field-group>
          <app-form-field
            label="Reference"
            hint="A transfer, deposit or cheque number."
            [optional]="true"
            [control]="form.controls.reference"
          >
            <input appControl type="text" formControlName="reference" maxlength="80" />
          </app-form-field>
          <app-form-field label="Note" [optional]="true" [control]="form.controls.note">
            <input appControl type="text" formControlName="note" maxlength="300" />
          </app-form-field>
        </form>
      </div>
      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          Cancel
        </button>
        <button appButton variant="primary" type="submit" form="payment-form" [busy]="saving()">
          Record payment
        </button>
      </div>
    </app-dialog>
  `,
  styles: `
    .choice-list--inline {
      grid-template-columns: repeat(auto-fit, minmax(9rem, 1fr));
    }
  `,
})
export class PaymentDialog implements OnInit {
  readonly bill = input.required<Bill>();

  readonly saved = output<Bill>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(BillingApi);
  private readonly session = inject(SessionState);
  private readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();
  private readonly requestId = new ClientRequestId();

  protected readonly methods = PAYMENT_METHODS;
  protected readonly today = todayIn(this.session.timeZoneId());
  protected readonly form = new FormGroup({
    amount: new FormControl<number | null>(null, [Validators.required, Validators.min(1)]),
    paidOn: new FormControl(this.today, { nonNullable: true, validators: [Validators.required] }),
    method: new FormControl<PaymentMethod>('BankTransfer', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    reference: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(80)] }),
    note: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(300)] }),
  });
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);

  protected readonly title = computed(() => `Record a payment on ${billName(this.bill())}`);
  protected readonly amountHint = computed(
    () =>
      `Still to collect on this bill: ${formatMoney(this.bill().outstandingMinor, this.session.currencyCode())}.`,
  );

  ngOnInit(): void {
    this.form.controls.amount.setValue(this.bill().outstandingMinor);
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
      const bill = this.bill();
      const saved = await firstValueFrom(
        this.api.recordPayment(bill.id, {
          amountMinor: value.amount ?? 0,
          paidOn: value.paidOn,
          method: value.method,
          reference: value.reference.trim() || null,
          note: value.note.trim() || null,
          clientRequestId: this.requestId.current,
          version: bill.version,
        }),
      );
      this.requestId.renew();
      this.saved.emit(saved);
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
