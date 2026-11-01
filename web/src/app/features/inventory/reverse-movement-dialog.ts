import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { formatDate } from '../../shared/presentation/business-time';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { quantityWithUnit } from './inventory-presentation';
import { InventoryItem, StockMovement } from './inventory.models';
import { MOVEMENT_TYPE, movementEnds, reversalSummary } from './movement-presentation';
import { StockActionDialog } from './stock-action-dialog';
import { notBlank } from './stock-validators';

/**
 * Undoes one line of an item's history with an equal and opposite one: the history is never edited. States what will happen
 * (including that a charge to a project is cancelled), asks for the reason, and shows the server's own words when the stock is
 * no longer where the line put it.
 */
@Component({
  selector: 'app-reverse-movement-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Reverse this movement?" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <div class="stack stack--tight">
          <p>
            <strong>{{ line().what }}</strong>
          </p>
          <p class="text-muted">{{ line().when }}</p>
          <p>{{ line().effect }}</p>
        </div>

        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extraMessages()"
            reloadLabel="Reload item"
            (reload)="reload()"
          />
        }

        <form id="reverse-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <app-form-field
            label="Reason"
            [control]="form.controls.reason"
            requiredMessage="Say why this movement is being reversed."
          >
            <textarea
              appControl
              formControlName="reason"
              rows="2"
              maxlength="300"
              placeholder="e.g. Moved to the wrong site"
            ></textarea>
          </app-form-field>
        </form>
      </div>

      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          Cancel
        </button>
        <button appButton variant="danger" type="submit" form="reverse-form" [busy]="saving()">
          Reverse movement
        </button>
      </div>
    </app-dialog>
  `,
})
export class ReverseMovementDialog extends StockActionDialog {
  /** The line of the history to reverse. */
  readonly movement = input.required<StockMovement>();

  protected readonly form = new FormGroup({
    reason: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, notBlank, Validators.maxLength(300)],
    }),
  });

  protected readonly line = computed(() => {
    const movement = this.movement();
    const item = this.item();
    const { from, to } = movementEnds(movement);
    return {
      what: `${MOVEMENT_TYPE[movement.type].label}: ${quantityWithUnit(movement.quantity, item.unit)}, ${from} to ${to}`,
      when: `Recorded for ${formatDate(movement.date)} by ${movement.recordedBy}`,
      effect: reversalSummary(movement, item, this.session.currencyCode()),
    };
  });

  protected override send(item: InventoryItem, requestId: string): Observable<InventoryItem> {
    return this.api.reverse(item.id, this.movement().id, {
      reason: this.form.getRawValue().reason.trim(),
      clientRequestId: requestId,
      version: item.version,
    });
  }
}
