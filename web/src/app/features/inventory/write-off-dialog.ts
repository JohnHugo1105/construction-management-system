import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { NumberInput } from '../../shared/ui/inputs/number-input';
import { balanceName, categoryRules, quantityWithUnit } from './inventory-presentation';
import { InventoryItem } from './inventory.models';
import { StockActionDialog } from './stock-action-dialog';
import { balanceKey, toLocation } from './stock-places';
import { greaterThanZero, notBlank } from './stock-validators';

/**
 * Writes off stock that was lost, broken or used up. Only places that hold some are offered. Writing off a single piece retires
 * it, so the dialog says so before the person confirms; reversing the write-off brings it back.
 */
@Component({
  selector: 'app-write-off-dialog',
  imports: [
    ReactiveFormsModule,
    Dialog,
    Button,
    FormField,
    Control,
    NumberInput,
    Alert,
    ApiErrorAlert,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Write off stock" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>
          <strong>{{ item().description }}</strong>
          <span class="text-muted"> · Stock that was lost, broken or used up.</span>
        </p>

        @if (singlePiece()) {
          <app-alert tone="warning" title="This will retire the item">
            This is a single piece. Writing it off retires it, so it can't be moved, counted or
            edited. You can bring it back by reversing the write-off.
          </app-alert>
        }

        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extraMessages()"
            reloadLabel="Reload item"
            (reload)="reload()"
          />
        }

        <form
          id="write-off-form"
          class="form"
          [formGroup]="form"
          (ngSubmit)="submit()"
          #formElement
        >
          <app-form-field
            label="Where is it?"
            [control]="form.controls.place"
            requiredMessage="Choose where the stock was."
          >
            <select appControl formControlName="place">
              <option value="">Choose a place</option>
              @for (option of placeOptions(); track option.value) {
                <option [value]="option.value">{{ option.label }}</option>
              }
            </select>
          </app-form-field>

          @if (!singlePiece()) {
            <app-form-field
              label="Quantity"
              [control]="form.controls.quantity"
              requiredMessage="Enter how many to write off."
            >
              <app-number-input formControlName="quantity" [decimals]="4" [unit]="item().unit" />
            </app-form-field>
          }

          <app-form-field
            label="Reason"
            [control]="form.controls.reason"
            requiredMessage="Say why the stock is being written off."
          >
            <textarea
              appControl
              formControlName="reason"
              rows="2"
              maxlength="300"
              placeholder="e.g. Broken handle, not repairable"
            ></textarea>
          </app-form-field>
        </form>
      </div>

      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          Cancel
        </button>
        <button appButton variant="danger" type="submit" form="write-off-form" [busy]="saving()">
          Write off
        </button>
      </div>
    </app-dialog>
  `,
})
export class WriteOffDialog extends StockActionDialog {
  protected override readonly serverFieldNames = { location: 'place' } as const;

  protected readonly form = new FormGroup({
    place: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    quantity: new FormControl<number | null>(null),
    reason: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, notBlank, Validators.maxLength(300)],
    }),
  });

  protected readonly singlePiece = computed(() => categoryRules(this.item().category).singlePiece);

  /** Only places that hold some, with how much. */
  protected readonly placeOptions = computed(() =>
    this.item().balances.map((balance) => ({
      value: balanceKey(balance),
      label: `${balanceName(balance)} (${quantityWithUnit(balance.quantity, this.item().unit)})`,
    })),
  );

  protected override prepare(item: InventoryItem): void {
    const rules = categoryRules(item.category);
    this.form.controls.quantity.setValidators(
      rules.singlePiece ? [] : [Validators.required, greaterThanZero],
    );
    this.form.controls.quantity.updateValueAndValidity({ emitEvent: false });
    const [only, ...others] = item.balances;
    if (only && others.length === 0) this.form.controls.place.setValue(balanceKey(only));
  }

  protected override send(item: InventoryItem, requestId: string): Observable<InventoryItem> {
    const value = this.form.getRawValue();
    return this.api.writeOff(item.id, {
      location: toLocation(value.place),
      quantity: categoryRules(item.category).fixedQuantity ?? value.quantity ?? 0,
      reason: value.reason.trim(),
      clientRequestId: requestId,
      version: item.version,
    });
  }
}
