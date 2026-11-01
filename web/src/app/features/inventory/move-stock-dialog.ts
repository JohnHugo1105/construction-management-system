import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable, map, startWith } from 'rxjs';
import { formatDate } from '../../shared/presentation/business-time';
import { formatMoney } from '../../shared/presentation/money';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { NumberInput } from '../../shared/ui/inputs/number-input';
import { chargePreviewMinor } from './charge-preview';
import {
  WAREHOUSE_NAME,
  balanceName,
  categoryRules,
  quantityWithUnit,
} from './inventory-presentation';
import { InventoryItem } from './inventory.models';
import { StockActionDialog } from './stock-action-dialog';
import { StockPlaces, WAREHOUSE_KEY, balanceKey, isProjectKey, toLocation } from './stock-places';
import { dateNotAfter, dateNotBefore, greaterThanZero } from './stock-validators';

/**
 * Moves stock from one place to another. The From list holds only places that have some (with how much); a single piece is
 * always 1, so its quantity is not asked. Moving to a project can be charged to it: the form shows a PREVIEW of the amount
 * (quantity times the item's unit cost, worked out with exact digits), and the server works out and saves the real one.
 *
 * With `returnFrom` set (the shortcut on a project's own page) the places are fixed: everything on that project goes back to the
 * warehouse unless the person changes the quantity.
 */
@Component({
  selector: 'app-move-stock-dialog',
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
    <app-dialog [title]="title()" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p class="item-line">
          <strong>{{ item().description }}</strong>
          @if (item().serialNumber) {
            <span class="text-muted"> · Serial {{ item().serialNumber }}</span>
          }
        </p>

        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extraMessages()"
            reloadLabel="Reload item"
            (reload)="reload()"
          />
        }

        <form id="move-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          @if (returnFrom()) {
            <dl class="def-list">
              <div>
                <dt>From</dt>
                <dd>{{ fixedFrom() }}</dd>
              </div>
              <div>
                <dt>To</dt>
                <dd>{{ warehouseName }}</dd>
              </div>
            </dl>
          } @else {
            <app-form-field
              label="From"
              [control]="form.controls.from"
              requiredMessage="Choose where it is coming from."
            >
              <select appControl formControlName="from">
                <option value="">Choose a place</option>
                @for (option of fromOptions(); track option.value) {
                  <option [value]="option.value">{{ option.label }}</option>
                }
              </select>
            </app-form-field>

            <app-form-field
              label="To"
              [control]="form.controls.to"
              requiredMessage="Choose where it is going."
            >
              <select appControl formControlName="to">
                <option value="">Choose a place</option>
                <option [value]="warehouseKey">{{ warehouseName }}</option>
                @for (option of openProjects(); track option.value) {
                  <option [value]="option.value">{{ option.label }}</option>
                }
                @if (archivedProjects().length > 0) {
                  <optgroup label="Archived projects (can't receive stock)">
                    @for (option of archivedProjects(); track option.value) {
                      <option [value]="option.value">{{ option.label }}</option>
                    }
                  </optgroup>
                }
              </select>
            </app-form-field>

            @if (toIsArchived()) {
              <app-alert tone="warning" title="This project is archived">
                {{ toName() }} can't receive stock until it is restored. Choose another place, or
                ask a project manager to restore it.
              </app-alert>
            }
          }

          @if (singlePiece()) {
            <p class="text-muted text-small">This is a single piece, so 1 is moved.</p>
          } @else {
            <app-form-field
              label="Quantity"
              [control]="form.controls.quantity"
              requiredMessage="Enter how many to move."
            >
              <app-number-input formControlName="quantity" [decimals]="4" [unit]="item().unit" />
            </app-form-field>
          }

          @if (canCharge()) {
            <label class="choice">
              <input type="checkbox" formControlName="charge" (change)="chargeToggled($event)" />
              <span class="choice__text">
                <span class="choice__label">Charge this move to the project</span>
                <span class="choice__hint"
                  >Adds the cost to the project's costs at the item's unit cost. Reversing the move
                  cancels it.</span
                >
              </span>
            </label>
            @if (preview(); as amount) {
              @if (amount.minor > 0) {
                <app-alert class="charge-preview" tone="info" title="Preview">
                  This move will charge {{ amount.text }} to {{ toName() }}. The saved amount is
                  worked out when you move the stock.
                </app-alert>
              } @else {
                <app-alert class="charge-preview" tone="warning" title="Preview">
                  At this quantity the charge works out to nothing. Turn the charge off, or check
                  the item's unit cost.
                </app-alert>
              }
            }
          } @else if (hasNoCost()) {
            <p class="text-muted text-small">
              This item has no cost on record, so this move can't be charged to the project.
            </p>
          }

          <app-form-field
            label="Date"
            [control]="form.controls.date"
            requiredMessage="Choose the date of the move."
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
        <button appButton variant="primary" type="submit" form="move-form" [busy]="saving()">
          Move stock
        </button>
      </div>
    </app-dialog>
  `,
})
export class MoveStockDialog extends StockActionDialog {
  /** The key of the project to take everything back from, when the dialog is the "Move to the warehouse" shortcut. */
  readonly returnFrom = input<string | null>(null);

  private readonly places = inject(StockPlaces);

  /** The API names the places `fromLocation` and `toLocation`. A message about the charge has no field of its own and is shown above the form. */
  protected override readonly serverFieldNames = {
    fromLocation: 'from',
    toLocation: 'to',
  } as const;

  protected readonly warehouseKey = WAREHOUSE_KEY;
  protected readonly warehouseName = WAREHOUSE_NAME;

  protected readonly form = new FormGroup({
    from: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    to: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    quantity: new FormControl<number | null>(null),
    date: new FormControl(this.todayDate, {
      nonNullable: true,
      validators: [Validators.required],
    }),
    charge: new FormControl(false, { nonNullable: true }),
    note: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(300)] }),
  });

  private readonly values = toSignal(
    this.form.valueChanges.pipe(
      map(() => this.form.getRawValue()),
      startWith(this.form.getRawValue()),
    ),
    { requireSync: true },
  );

  protected readonly title = computed(() =>
    this.returnFrom() ? 'Move to the warehouse' : 'Move stock',
  );
  protected readonly singlePiece = computed(() => categoryRules(this.item().category).singlePiece);

  protected readonly fromOptions = computed(() =>
    this.item().balances.map((balance) => ({
      value: balanceKey(balance),
      label: `${balanceName(balance)} (${quantityWithUnit(balance.quantity, this.item().unit)})`,
    })),
  );

  /** For the shortcut: where it is leaving, with how much is there. */
  protected readonly fixedFrom = computed(() => {
    const balance = this.item().balances.find((entry) => balanceKey(entry) === this.returnFrom());
    return balance
      ? `${balanceName(balance)} (${quantityWithUnit(balance.quantity, this.item().unit)})`
      : '';
  });

  /** The place it leaves is never offered as the place it arrives. */
  protected readonly openProjects = computed(() =>
    this.places.open().filter((place) => place.value !== this.values().from),
  );
  protected readonly archivedProjects = computed(() =>
    this.places.archived().filter((place) => place.value !== this.values().from),
  );

  protected readonly toIsArchived = computed(() => this.places.isArchived(this.values().to));
  protected readonly toName = computed(() => this.places.nameOf(this.values().to));
  private readonly toIsProject = computed(
    () => isProjectKey(this.values().to) && !this.toIsArchived(),
  );

  protected readonly hasNoCost = computed(
    () => this.toIsProject() && this.item().unitCostMinor <= 0,
  );
  protected readonly canCharge = computed(
    () => this.toIsProject() && this.item().unitCostMinor > 0,
  );

  /** "This move will charge ₱6,300.00 to Riverside": a preview, worked out from the typed quantity and the item's unit cost. */
  protected readonly preview = computed(() => {
    const values = this.values();
    if (!this.canCharge() || !values.charge) return null;
    const quantity = this.singlePiece() ? 1 : values.quantity;
    const minor = chargePreviewMinor(quantity, this.item().unitCostMinor);
    if (minor === null) return null;
    return { minor, text: formatMoney(minor, this.session.currencyCode()) };
  });

  /** The amount appears below the tick box, often under the fold of a small screen: bring it into view. */
  protected chargeToggled(event: Event): void {
    if ((event.target as HTMLInputElement).checked) this.reveal('.charge-preview');
  }

  protected override prepare(item: InventoryItem): void {
    const { from, to, quantity, date } = this.form.controls;
    date.setValidators([
      Validators.required,
      dateNotAfter(this.todayDate, "The date can't be in the future."),
      dateNotBefore(
        item.purchaseDate,
        `The date can't be before the purchase date (${formatDate(item.purchaseDate)}).`,
      ),
    ]);
    date.updateValueAndValidity({ emitEvent: false });

    const rules = categoryRules(item.category);
    quantity.setValidators(rules.singlePiece ? [] : [Validators.required, greaterThanZero]);
    quantity.updateValueAndValidity({ emitEvent: false });

    const leaving = this.returnFrom();
    if (leaving) {
      from.setValue(leaving);
      to.setValue(WAREHOUSE_KEY);
      const there = item.balances.find((balance) => balanceKey(balance) === leaving);
      if (!rules.singlePiece && there) quantity.setValue(there.quantity);
      return;
    }

    this.places.load();
    // With only one place to take it from there is nothing to choose.
    const [only, ...others] = item.balances;
    if (only && others.length === 0) from.setValue(balanceKey(only));
  }

  protected override send(item: InventoryItem, requestId: string): Observable<InventoryItem> {
    const value = this.form.getRawValue();
    return this.api.transfer(item.id, {
      fromLocation: toLocation(value.from),
      toLocation: toLocation(value.to),
      quantity: categoryRules(item.category).fixedQuantity ?? value.quantity ?? 0,
      date: value.date,
      chargeToProject: this.canCharge() && value.charge,
      note: value.note.trim() || null,
      clientRequestId: requestId,
      version: item.version,
    });
  }
}
