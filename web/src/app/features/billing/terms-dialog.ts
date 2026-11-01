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
import { injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { FormField } from '../../shared/ui/form-field/form-field';
import { NumberInput } from '../../shared/ui/inputs/number-input';
import { PercentInput } from '../../shared/ui/inputs/percent-input';
import { describeFormFailure } from '../workforce/form-failure';
import { BillingApi } from './billing-api';
import { BillingTerms } from './billing.models';

/** Edits how a project is billed. The new terms apply to bills prepared from now on; bills already made keep theirs. */
@Component({
  selector: 'app-terms-dialog',
  imports: [
    ReactiveFormsModule,
    Dialog,
    Button,
    FormField,
    PercentInput,
    NumberInput,
    ApiErrorAlert,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Billing terms" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p class="text-muted text-small">
          These apply to bills prepared from now on. Bills already prepared keep the terms they were
          made with.
        </p>
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            [allowRetry]="problem.status === 409"
            reloadLabel="Reload"
            (reload)="reloaded.emit()"
          />
        }
        <form id="terms-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <app-form-field
            label="Retention"
            hint="The share of each progress bill the client holds until the project is completed. Up to 30%."
            [control]="form.controls.retentionBps"
            requiredMessage="Enter the retention, or 0 for none."
          >
            <app-percent-input formControlName="retentionBps" data-autofocus />
          </app-form-field>
          <app-form-field
            label="Creditable withholding tax"
            hint="The share of the amount before VAT the client withholds and covers with a certificate. Up to 10%."
            [control]="form.controls.creditableWithholdingBps"
            requiredMessage="Enter the withholding, or 0 for none."
          >
            <app-percent-input formControlName="creditableWithholdingBps" />
          </app-form-field>
          <app-form-field
            label="Payment due"
            hint="Days after the billing date, up to 180."
            [control]="form.controls.paymentTermsDays"
            requiredMessage="Enter the number of days, or 0 for due on billing."
          >
            <app-number-input formControlName="paymentTermsDays" [decimals]="0" unit="days" />
          </app-form-field>
        </form>
      </div>
      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          Cancel
        </button>
        <button appButton variant="primary" type="submit" form="terms-form" [busy]="saving()">
          Save billing terms
        </button>
      </div>
    </app-dialog>
  `,
})
export class TermsDialog implements OnInit {
  readonly projectId = input.required<string>();
  readonly terms = input.required<BillingTerms>();

  readonly saved = output<BillingTerms>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(BillingApi);
  private readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  protected readonly form = new FormGroup({
    retentionBps: new FormControl<number | null>(null, [
      Validators.required,
      Validators.min(0),
      Validators.max(3_000),
    ]),
    creditableWithholdingBps: new FormControl<number | null>(null, [
      Validators.required,
      Validators.min(0),
      Validators.max(1_000),
    ]),
    paymentTermsDays: new FormControl<number | null>(null, [
      Validators.required,
      Validators.min(0),
      Validators.max(180),
    ]),
  });
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);

  ngOnInit(): void {
    const terms = this.terms();
    this.form.setValue({
      retentionBps: terms.retentionBps,
      creditableWithholdingBps: terms.creditableWithholdingBps,
      paymentTermsDays: terms.paymentTermsDays,
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
      this.saved.emit(
        await firstValueFrom(
          this.api.saveTerms(this.projectId(), {
            retentionBps: value.retentionBps ?? 0,
            creditableWithholdingBps: value.creditableWithholdingBps ?? 0,
            paymentTermsDays: value.paymentTermsDays ?? 0,
            version: this.terms().version,
          }),
        ),
      );
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
