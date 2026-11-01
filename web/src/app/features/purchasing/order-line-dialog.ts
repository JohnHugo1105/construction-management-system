import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, output, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { SessionState } from '../../core/session/session-state';
import { formatMoney } from '../../shared/presentation/money';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FieldGroup } from '../../shared/ui/form-field/field-group';
import { FormField } from '../../shared/ui/form-field/form-field';
import { MoneyInput } from '../../shared/ui/inputs/money-input';
import { NumberInput } from '../../shared/ui/inputs/number-input';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { CostingReferenceState } from '../costing/costing-reference';
import { ItemType, VatTreatment } from '../costing/costing.models';
import { lineTotalMinor } from '../costing/line-amount';
import { PurchaseOrderLineFields } from './purchasing.models';

/** The standard code a line is classified under, or '' for none. A line of an order uses the standard list only. */
function codeOf(line: PurchaseOrderLineFields | null): string {
  return line?.wbsCode ?? '';
}

/**
 * Adds or changes one line of a purchase order, in a dialog, the way a cost line is entered: what is bought, how many, at what unit
 * cost, and whether VAT is in the price. The "Line total" is a quick check; the saved net, VAT and total come from the server
 * when the order is saved. The dialog keeps the line in the order form, it does not save anything itself.
 */
@Component({
  selector: 'app-order-line-dialog',
  imports: [
    ReactiveFormsModule,
    Dialog,
    Button,
    Alert,
    ApiErrorAlert,
    FormField,
    FieldGroup,
    Control,
    MoneyInput,
    NumberInput,
    Skeleton,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './order-line-dialog.html',
  styleUrl: './order-line-dialog.scss',
})
export class OrderLineDialog implements OnInit {
  /** The line being changed; leave it out to add one. */
  readonly line = input<PurchaseOrderLineFields | null>(null);

  readonly saved = output<PurchaseOrderLineFields>();
  readonly dismissed = output<void>();

  private readonly session = inject(SessionState);
  protected readonly referenceState = inject(CostingReferenceState);
  private readonly dialog = viewChild.required(Dialog);

  protected readonly reference = this.referenceState.reference;
  protected readonly showSummary = signal(false);
  protected readonly previewMinor = signal<number | null>(null);
  protected readonly division = signal('');

  protected readonly form = new FormGroup({
    description: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
    itemType: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    division: new FormControl('', { nonNullable: true }),
    wbsCode: new FormControl('', { nonNullable: true }),
    quantity: new FormControl<number | null>(null, { validators: [Validators.required, Validators.min(0.0001)] }),
    unit: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(20)] }),
    unitCostMinor: new FormControl<number | null>(null, { validators: [Validators.required, Validators.min(0)] }),
    vatTreatment: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  protected readonly scopes = computed(
    () => this.reference()?.divisions.find((item) => String(item.number) === this.division())?.scopes ?? [],
  );
  protected readonly previewText = computed(() => {
    const minor = this.previewMinor();
    return minor === null ? null : formatMoney(minor, this.session.currencyCode());
  });
  protected readonly title = computed(() => (this.line() ? 'Change line' : 'Add a line'));
  protected readonly vatHint = computed(
    () => `VAT is worked out at the company rate of ${(this.session.vatRateBps() / 100).toString()}% when you save the order.`,
  );

  constructor() {
    void this.referenceState.load();
    this.form.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => {
      const value = this.form.getRawValue();
      this.previewMinor.set(lineTotalMinor(value.quantity, value.unitCostMinor));
      this.division.set(value.division);
    });
  }

  ngOnInit(): void {
    const line = this.line();
    if (!line) return;
    this.form.reset({
      description: line.description,
      itemType: line.itemType,
      division: line.division === null ? '' : String(line.division),
      wbsCode: codeOf(line),
      quantity: line.quantity,
      unit: line.unit,
      unitCostMinor: line.unitCostMinor,
      vatTreatment: line.vatTreatment,
    });
  }

  protected divisionChanged(): void {
    this.form.controls.wbsCode.setValue('');
  }

  protected retryReference(): void {
    void this.referenceState.load();
  }

  protected cancel(): void {
    this.dialog().dismiss();
  }

  protected submit(): void {
    const reference = this.reference();
    if (!reference) return;
    this.form.markAllAsTouched();
    const value = this.form.getRawValue();
    if (this.form.invalid || value.description.trim() === '' || value.unit.trim() === '') {
      this.showSummary.set(true);
      return;
    }
    const division = value.division === '' ? null : Number(value.division);
    const owner = reference.divisions.find((item) => item.number === division);
    const scope = owner?.scopes.find((item) => item.code === value.wbsCode);
    this.saved.emit({
      division,
      mainScope: owner?.mainScope ?? null,
      subScope: scope?.name ?? null,
      wbsCode: scope?.code ?? null,
      description: value.description.trim(),
      itemType: value.itemType as ItemType,
      quantity: value.quantity ?? 0,
      unit: value.unit.trim(),
      unitCostMinor: value.unitCostMinor ?? 0,
      vatTreatment: value.vatTreatment as VatTreatment,
    });
  }
}
