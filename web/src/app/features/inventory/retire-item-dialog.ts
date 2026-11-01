import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { quantityWithUnit } from './inventory-presentation';
import { InventoryItem } from './inventory.models';
import { StockActionDialog } from './stock-action-dialog';
import { notBlank } from './stock-validators';

/**
 * Takes an item out of use. It can only be retired with no stock left; with stock still on record the dialog explains what to do
 * first instead of showing a form that cannot work.
 */
@Component({
  selector: 'app-retire-item-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, Alert, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Retire this item?" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>
          <strong>{{ item().description }}</strong>
        </p>

        @if (stillHasStock()) {
          <app-alert tone="warning" title="There is still stock">
            Stock is still on record ({{ stockLeft() }}). Move it out or write it off first, then
            retire the item.
          </app-alert>
        } @else {
          <p>
            A retired item can't be moved, counted or edited, and it leaves the active list. It
            stays on record.
          </p>

          @if (error(); as problem) {
            <app-api-error-alert
              [error]="problem"
              [extra]="extraMessages()"
              reloadLabel="Reload item"
              (reload)="reload()"
            />
          }

          <form id="retire-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
            <app-form-field
              label="Reason"
              [control]="form.controls.reason"
              requiredMessage="Say why the item is being retired."
            >
              <textarea
                appControl
                formControlName="reason"
                rows="2"
                maxlength="300"
                placeholder="e.g. Sold to another company"
              ></textarea>
            </app-form-field>
          </form>
        }
      </div>

      <div dialogActions class="dialog-actions">
        @if (stillHasStock()) {
          <button appButton variant="primary" type="button" (click)="cancel()">Close</button>
        } @else {
          <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
            Cancel
          </button>
          <button appButton variant="danger" type="submit" form="retire-form" [busy]="saving()">
            Retire item
          </button>
        }
      </div>
    </app-dialog>
  `,
})
export class RetireItemDialog extends StockActionDialog {
  protected readonly form = new FormGroup({
    reason: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, notBlank, Validators.maxLength(300)],
    }),
  });

  protected readonly stillHasStock = computed(() => this.item().totalQuantity > 0);
  protected readonly stockLeft = computed(() =>
    quantityWithUnit(this.item().totalQuantity, this.item().unit),
  );

  protected override send(item: InventoryItem): Observable<InventoryItem> {
    return this.api.retire(item.id, {
      reason: this.form.getRawValue().reason.trim(),
      version: item.version,
    });
  }
}
