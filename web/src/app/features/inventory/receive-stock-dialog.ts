import { ChangeDetectionStrategy, Component } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { formatDate } from '../../shared/presentation/business-time';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { NumberInput } from '../../shared/ui/inputs/number-input';
import { InventoryItem } from './inventory.models';
import { StockActionDialog } from './stock-action-dialog';
import { dateNotAfter, dateNotBefore, greaterThanZero } from './stock-validators';

/** Restocks a tool: more of the same item arrives in the warehouse. (A single piece is received once, when it is registered.) */
@Component({
  selector: 'app-receive-stock-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, NumberInput, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Receive more stock" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>
          <strong>{{ item().description }}</strong>
          <span class="text-muted"> · The new stock goes into the warehouse.</span>
        </p>

        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extraMessages()"
            reloadLabel="Reload item"
            (reload)="reload()"
          />
        }

        <form id="receive-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <app-form-field
            label="Quantity"
            [control]="form.controls.quantity"
            requiredMessage="Enter how many arrived."
          >
            <app-number-input formControlName="quantity" [decimals]="4" [unit]="item().unit" />
          </app-form-field>

          <app-form-field
            label="Date received"
            [control]="form.controls.date"
            requiredMessage="Choose the date it arrived."
          >
            <input
              appControl
              type="date"
              formControlName="date"
              [attr.min]="item().purchaseDate"
              [attr.max]="todayDate"
            />
          </app-form-field>

          <app-form-field label="Note" [optional]="true" [control]="form.controls.note">
            <textarea appControl formControlName="note" rows="2" maxlength="300"></textarea>
          </app-form-field>
        </form>
      </div>

      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          Cancel
        </button>
        <button appButton variant="primary" type="submit" form="receive-form" [busy]="saving()">
          Receive stock
        </button>
      </div>
    </app-dialog>
  `,
})
export class ReceiveStockDialog extends StockActionDialog {
  protected readonly form = new FormGroup({
    quantity: new FormControl<number | null>(null, [Validators.required, greaterThanZero]),
    date: new FormControl(this.todayDate, {
      nonNullable: true,
      validators: [Validators.required],
    }),
    note: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(300)] }),
  });

  protected override prepare(item: InventoryItem): void {
    const { date } = this.form.controls;
    date.setValidators([
      Validators.required,
      dateNotAfter(this.todayDate, "The date can't be in the future."),
      dateNotBefore(
        item.purchaseDate,
        `The date can't be before the purchase date (${formatDate(item.purchaseDate)}).`,
      ),
    ]);
    date.updateValueAndValidity({ emitEvent: false });
  }

  protected override send(item: InventoryItem, requestId: string): Observable<InventoryItem> {
    const value = this.form.getRawValue();
    return this.api.receive(item.id, {
      quantity: value.quantity ?? 0,
      date: value.date,
      note: value.note.trim() || null,
      clientRequestId: requestId,
      version: item.version,
    });
  }
}
