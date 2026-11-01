import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
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
import { PAYMENT_METHODS, claimName } from './subcontract-presentation';
import { SubcontractsApi } from './subcontracts-api';
import { PaymentMethod, Subcontract, SubcontractClaim } from './subcontracts.models';

/**
 * Records that a payment was made: against a certified claim, or a release of retention once the subcontract is complete. It is a
 * person saying it was paid; nothing is sent to the subcontractor or a bank. The amount starts at what the server says is still
 * owed and can be lowered for a part payment.
 */
@Component({
  selector: 'app-payment-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, FieldGroup, Control, MoneyInput, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog [title]="title()" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p class="text-small text-muted">{{ owedLine() }} This only records the payment; nothing is paid from here.</p>
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            [allowRetry]="problem.status === 409"
            reloadLabel="Reload subcontract"
            (reload)="reloaded.emit()"
          />
        }
        <form id="payment-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <div class="form__row">
            <app-form-field label="Amount paid" [control]="form.controls.amountMinor" requiredMessage="Enter the amount paid.">
              <app-money-input formControlName="amountMinor" data-autofocus />
            </app-form-field>
            <app-form-field label="Paid on" [control]="form.controls.paidOn" requiredMessage="Choose the date it was paid.">
              <input appControl type="date" formControlName="paidOn" [max]="today" />
            </app-form-field>
          </div>
          <app-field-group label="Paid by" [control]="form.controls.method">
            <div class="choice-list methods">
              @for (method of methods; track method.value) {
                <label class="choice">
                  <input type="radio" formControlName="method" [value]="method.value" />
                  <span class="choice__text"><span class="choice__label">{{ method.label }}</span></span>
                </label>
              }
            </div>
          </app-field-group>
          <app-form-field label="Reference" [optional]="true" hint="Cheque or transfer number." [control]="form.controls.reference">
            <input appControl formControlName="reference" maxlength="80" autocomplete="off" />
          </app-form-field>
          <app-form-field label="Note" [optional]="true" [control]="form.controls.note">
            <input appControl formControlName="note" maxlength="300" autocomplete="off" />
          </app-form-field>
        </form>
      </div>
      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="dialog().dismiss()">Cancel</button>
        <button appButton variant="primary" type="submit" form="payment-form" [busy]="saving()">Record payment</button>
      </div>
    </app-dialog>
  `,
  styles: `
    .methods {
      grid-template-columns: repeat(2, minmax(0, 1fr));
    }
  `,
})
export class PaymentDialog {
  readonly subcontract = input.required<Subcontract>();
  /** The claim paid; null records a retention release. */
  readonly claim = input<SubcontractClaim | null>(null);

  readonly saved = output<Subcontract>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(SubcontractsApi);
  private readonly session = inject(SessionState);
  protected readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();
  private readonly requestId = new ClientRequestId();
  protected readonly today = todayIn(this.session.timeZoneId());
  protected readonly methods = PAYMENT_METHODS;

  protected readonly form = new FormGroup({
    amountMinor: new FormControl<number | null>(null, { validators: [Validators.required] }),
    paidOn: new FormControl(this.today, { nonNullable: true, validators: [Validators.required] }),
    method: new FormControl<PaymentMethod>('BankTransfer', { nonNullable: true }),
    reference: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(80)] }),
    note: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(300)] }),
  });

  /** What the server says is still owed on the claim, or the retention still held. */
  private readonly owed = computed(() => this.claim()?.outstandingMinor ?? this.subcontract().retentionBalanceMinor);
  protected readonly title = computed(() => {
    const claim = this.claim();
    return claim ? `Record payment for ${claimName(claim).toLowerCase()}` : 'Release retention';
  });
  protected readonly owedLine = computed(() => {
    const amount = formatMoney(this.owed(), this.session.currencyCode());
    return this.claim() ? `${amount} is still owed on this claim.` : `${amount} of retention is still held.`;
  });

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);

  constructor() {
    queueMicrotask(() => this.form.controls.amountMinor.setValue(this.owed()));
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }
    const value = this.form.getRawValue();
    const payment = { ...value, reference: value.reference.trim() || null, note: value.note.trim() || null };
    const subcontract = this.subcontract();
    const claim = this.claim();
    this.saving.set(true);
    this.error.set(null);
    this.extra.set([]);
    try {
      const saved = claim
        ? await firstValueFrom(this.api.payClaim(subcontract.id, claim.id, payment, subcontract.version, this.requestId.current))
        : await firstValueFrom(this.api.releaseRetention(subcontract.id, payment, subcontract.version, this.requestId.current));
      this.requestId.renew();
      this.saved.emit(saved);
    } catch (failure) {
      const outcome = describeFormFailure(this.form, asApiError(failure));
      this.error.set(outcome.alert);
      this.extra.set(outcome.extra);
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }
}
