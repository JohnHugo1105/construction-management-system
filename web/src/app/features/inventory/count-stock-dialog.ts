import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { Observable, startWith } from 'rxjs';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { NumberInput } from '../../shared/ui/inputs/number-input';
import { WAREHOUSE_NAME, categoryRules, quantityWithUnit } from './inventory-presentation';
import { InventoryItem } from './inventory.models';
import { StockActionDialog } from './stock-action-dialog';
import { notBlank } from './stock-validators';
import { StockPlaces, WAREHOUSE_KEY, balanceKey, toLocation } from './stock-places';

/** A single piece is there (1) or it is not (0). */
function zeroOrOneForSinglePiece(single: () => boolean) {
  return (control: AbstractControl): ValidationErrors | null => {
    const value: unknown = control.value;
    return single() && typeof value === 'number' && value > 1
      ? { message: 'A single piece is counted as 0 or 1.' }
      : null;
  };
}

/**
 * A stock count: the person counted what is really at a place and says how many. The place shows what the records say, so the
 * difference is clear; the server records the difference as a count line and keeps the reason.
 */
@Component({
  selector: 'app-count-stock-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, NumberInput, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Count stock" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>
          <strong>{{ item().description }}</strong>
          <span class="text-muted">
            · Record what you counted. The records are corrected to match.</span
          >
        </p>

        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extraMessages()"
            reloadLabel="Reload item"
            (reload)="reload()"
          />
        }

        <form id="count-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <app-form-field
            label="Where did you count?"
            [control]="form.controls.place"
            requiredMessage="Choose where you counted."
            [hint]="recorded()"
          >
            <select appControl formControlName="place">
              <option value="">Choose a place</option>
              @for (option of placeOptions(); track option.value) {
                <option [value]="option.value">{{ option.label }}</option>
              }
            </select>
          </app-form-field>

          <app-form-field
            label="How many did you count?"
            [hint]="singlePiece() ? 'Count 1 if the piece is there, or 0 if it is not.' : ''"
            [control]="form.controls.counted"
            requiredMessage="Enter how many you counted. Use 0 if there are none."
          >
            <app-number-input formControlName="counted" [decimals]="4" [unit]="item().unit" />
          </app-form-field>

          <app-form-field
            label="Reason"
            [control]="form.controls.reason"
            requiredMessage="Say why the count is different."
          >
            <textarea
              appControl
              formControlName="reason"
              rows="2"
              maxlength="300"
              placeholder="e.g. Counted again on site"
            ></textarea>
          </app-form-field>
        </form>
      </div>

      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          Cancel
        </button>
        <button appButton variant="primary" type="submit" form="count-form" [busy]="saving()">
          Record count
        </button>
      </div>
    </app-dialog>
  `,
})
export class CountStockDialog extends StockActionDialog {
  private readonly places = inject(StockPlaces);

  protected override readonly serverFieldNames = {
    location: 'place',
    countedQuantity: 'counted',
  } as const;

  protected readonly form = new FormGroup({
    place: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    counted: new FormControl<number | null>(null, [Validators.required]),
    reason: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, notBlank, Validators.maxLength(300)],
    }),
  });

  private readonly chosen = toSignal(
    this.form.controls.place.valueChanges.pipe(startWith(this.form.controls.place.value)),
    { requireSync: true },
  );

  protected readonly singlePiece = computed(() => categoryRules(this.item().category).singlePiece);

  /** Every place a count can be made, with what the records say where they say something. */
  protected readonly placeOptions = computed(() => {
    const item = this.item();
    const recorded = new Map(item.balances.map((balance) => [balanceKey(balance), balance]));
    const label = (key: string, name: string) => {
      const balance = recorded.get(key);
      return balance ? `${name} (${quantityWithUnit(balance.quantity, item.unit)})` : name;
    };
    return [
      { value: WAREHOUSE_KEY, label: label(WAREHOUSE_KEY, WAREHOUSE_NAME) },
      ...this.places.everyProject().map((place) => ({
        value: place.value,
        label: label(
          place.value,
          this.places.isArchived(place.value) ? `${place.label} (archived)` : place.label,
        ),
      })),
    ];
  });

  /** What the records say about the chosen place, so the person sees what their count changes. */
  protected readonly recorded = computed(() => {
    const key = this.chosen();
    if (!key) return '';
    const item = this.item();
    const balance = item.balances.find((entry) => balanceKey(entry) === key);
    return balance
      ? `The records say ${quantityWithUnit(balance.quantity, item.unit)} here.`
      : 'The records say none here.';
  });

  protected override prepare(item: InventoryItem): void {
    this.places.load();
    this.form.controls.counted.setValidators([
      Validators.required,
      zeroOrOneForSinglePiece(() => this.singlePiece()),
    ]);
    this.form.controls.counted.updateValueAndValidity({ emitEvent: false });
    // With stock in only one place, that is the place to count.
    const [only, ...others] = item.balances;
    if (only && others.length === 0) this.form.controls.place.setValue(balanceKey(only));
  }

  protected override send(item: InventoryItem, requestId: string): Observable<InventoryItem> {
    const value = this.form.getRawValue();
    return this.api.adjust(item.id, {
      location: toLocation(value.place),
      countedQuantity: value.counted ?? 0,
      reason: value.reason.trim(),
      clientRequestId: requestId,
      version: item.version,
    });
  }
}
