import { ChangeDetectionStrategy, Component, inject, input, output, signal, viewChild } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { PurchasingApi } from './purchasing-api';
import { PurchaseOrder } from './purchasing.models';

/**
 * Closes an approved order because the supplier delivered less than was ordered. The reason is optional. What was not invoiced is
 * released from committed cost, and the order can't be opened again, so the dialog says so before asking.
 */
@Component({
  selector: 'app-close-order-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Close this order?" [locked]="busy()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>
          Close {{ order().number }} when the supplier delivered less than you ordered. What has not
          been invoiced stops counting as committed cost. A closed order can't be opened again.
        </p>
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            reloadLabel="Reload order"
            (reload)="reloadRequested.emit()"
          />
        }
        <form id="close-form" class="form" [formGroup]="form" (ngSubmit)="submit()">
          <app-form-field label="Reason" [optional]="true" [control]="form.controls.reason">
            <textarea
              appControl
              formControlName="reason"
              rows="2"
              maxlength="300"
              placeholder="e.g. The supplier could deliver only part of it"
            ></textarea>
          </app-form-field>
        </form>
      </div>
      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="busy() ? '' : null" (click)="cancel()">
          Cancel
        </button>
        <button appButton variant="primary" type="submit" form="close-form" [busy]="busy()">
          Close order
        </button>
      </div>
    </app-dialog>
  `,
})
export class CloseOrderDialog {
  readonly order = input.required<PurchaseOrder>();
  readonly done = output<void>();
  readonly dismissed = output<void>();
  readonly reloadRequested = output<void>();

  private readonly api = inject(PurchasingApi);
  private readonly dialog = viewChild.required(Dialog);

  protected readonly form = new FormGroup({
    reason: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(300)] }),
  });
  protected readonly busy = signal(false);
  protected readonly error = signal<ApiError | null>(null);

  protected cancel(): void {
    this.dialog().dismiss();
  }

  protected async submit(): Promise<void> {
    if (this.busy() || this.form.invalid) return;
    this.busy.set(true);
    this.error.set(null);
    const reason = this.form.controls.reason.value.trim();
    try {
      const current = this.order();
      await firstValueFrom(this.api.closeOrder(current.id, reason === '' ? null : reason, current.version));
      this.done.emit();
    } catch (failure) {
      this.error.set(asApiError(failure));
    } finally {
      this.busy.set(false);
    }
  }
}
