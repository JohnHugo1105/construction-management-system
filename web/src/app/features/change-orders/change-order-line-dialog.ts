import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnInit,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { SessionState } from '../../core/session/session-state';
import { applyServerErrors, injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { todayIn } from '../../shared/presentation/business-time';
import { formatMoney } from '../../shared/presentation/money';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FieldGroup } from '../../shared/ui/form-field/field-group';
import { FormField } from '../../shared/ui/form-field/form-field';
import { MoneyInput } from '../../shared/ui/inputs/money-input';
import { NumberInput } from '../../shared/ui/inputs/number-input';
import { SearchBox } from '../../shared/ui/search-box/search-box';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { CostingApi } from '../costing/costing-api';
import { formatQuantity } from '../costing/costing-labels';
import { CostingReferenceState } from '../costing/costing-reference';
import { BudgetLine, ItemType, VatTreatment } from '../costing/costing.models';
import { ProjectContext } from '../projects/project-context';
import { ChangeOrdersApi } from './change-orders-api';
import {
  ChangeOrder,
  ChangeOrderLine,
  ChangeOrderLineFields,
  ChangeOrderLineKind,
} from './change-orders.models';

const PICKER_SIZE = 20;

const KINDS: readonly { value: ChangeOrderLineKind; label: string; hint: string }[] = [
  {
    value: 'Add',
    label: 'Add a new budget line',
    hint: 'Work or items that were not in the budget.',
  },
  {
    value: 'Revise',
    label: 'Revise a budget line',
    hint: 'A new quantity or unit cost for a line already in the budget.',
  },
  { value: 'Omit', label: 'Omit a budget line', hint: 'Work taken out of the scope.' },
];

/** A budget line as the picker lists it. */
export function describeBudgetLine(line: BudgetLine, currencyCode: string): string {
  return `${formatQuantity(line.quantity)} ${line.unit} × ${formatMoney(line.unitCostMinor, currencyCode)} = ${formatMoney(line.grossMinor, currencyCode)} with VAT`;
}

/**
 * Adds one line to a draft change order, or edits one: Add a new budget line, Revise the quantity and unit cost of a line in the
 * budget, or Omit one. Revise and Omit choose from the project's active budget lines (searchable). The effect is worked out by the
 * server and shown on the change order once saved.
 */
@Component({
  selector: 'app-change-order-line-dialog',
  imports: [
    ReactiveFormsModule,
    Dialog,
    Button,
    FormField,
    FieldGroup,
    Control,
    MoneyInput,
    NumberInput,
    SearchBox,
    Skeleton,
    ApiErrorAlert,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .picker {
      max-height: 16rem;
      overflow-y: auto;
    }
    .now {
      margin: 0;
    }
  `,
  template: `
    <app-dialog
      [title]="line() ? 'Edit line ' + line()!.lineNumber : 'Add a line'"
      [locked]="saving()"
      (closed)="dismissed.emit()"
    >
      <div class="stack">
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            (reload)="reloadRequested.emit()"
          />
        }
        <form id="co-line-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <app-field-group label="What does this line change?" [control]="form.controls.kind">
            <div class="choice-list">
              @for (choice of kinds; track choice.value) {
                <label class="choice">
                  <input type="radio" formControlName="kind" [value]="choice.value" />
                  <span class="choice__text">
                    <span class="choice__label">{{ choice.label }}</span>
                    <span class="choice__hint">{{ choice.hint }}</span>
                  </span>
                </label>
              }
            </div>
          </app-field-group>

          @if (kind() === 'Add') {
            @if (referenceState.reference(); as ref) {
              <app-form-field
                label="Description"
                [control]="form.controls.description"
                requiredMessage="Enter a description of the item."
              >
                <input
                  appControl
                  formControlName="description"
                  maxlength="200"
                  autocomplete="off"
                />
              </app-form-field>
              <div class="form__row">
                <app-form-field
                  label="Item type"
                  [control]="form.controls.itemType"
                  requiredMessage="Choose what this line pays for."
                >
                  <select appControl formControlName="itemType">
                    <option value="">Choose</option>
                    @for (type of ref.itemTypes; track type.value) {
                      <option [value]="type.value">{{ type.label }}</option>
                    }
                  </select>
                </app-form-field>
                <app-form-field
                  label="Division"
                  [optional]="true"
                  [control]="form.controls.division"
                >
                  <select appControl formControlName="division">
                    <option value="">Not classified</option>
                    @for (division of ref.divisions; track division.number) {
                      <option [value]="division.number">
                        {{ division.number }} · {{ division.mainScope }}
                      </option>
                    }
                  </select>
                </app-form-field>
              </div>
              @if (scopes().length > 0) {
                <app-form-field
                  label="Work item"
                  [control]="form.controls.wbsCode"
                  requiredMessage="Choose the work item."
                >
                  <select appControl formControlName="wbsCode">
                    <option value="">Choose a work item</option>
                    @for (scope of scopes(); track scope.code) {
                      <option [value]="scope.code">{{ scope.code }} · {{ scope.name }}</option>
                    }
                  </select>
                </app-form-field>
              }
              @if (subProjects().length > 0) {
                <app-form-field label="Part of the project" [control]="form.controls.subProjectId">
                  <select appControl formControlName="subProjectId">
                    <option value="">The whole project</option>
                    @for (sub of subProjects(); track sub.id) {
                      <option [value]="sub.id">{{ sub.name }}</option>
                    }
                  </select>
                </app-form-field>
              }
              <div class="form__row">
                <app-form-field
                  label="Quantity"
                  [control]="form.controls.quantity"
                  requiredMessage="Enter the quantity."
                >
                  <app-number-input formControlName="quantity" [decimals]="4" placeholder="0" />
                </app-form-field>
                <app-form-field
                  label="Unit"
                  [control]="form.controls.unit"
                  requiredMessage="Enter the unit, for example pc, bag or m³."
                >
                  <input
                    appControl
                    formControlName="unit"
                    maxlength="20"
                    autocomplete="off"
                    placeholder="e.g. bag"
                  />
                </app-form-field>
              </div>
              <app-form-field
                label="Unit cost"
                [control]="form.controls.unitCostMinor"
                requiredMessage="Enter the cost of one unit."
              >
                <app-money-input formControlName="unitCostMinor" placeholder="0.00" />
              </app-form-field>
              <app-field-group
                label="VAT"
                [control]="form.controls.vatTreatment"
                requiredMessage="Choose whether the unit cost includes VAT."
              >
                <div class="choice-list">
                  @for (treatment of ref.vatTreatments; track treatment.value) {
                    <label class="choice">
                      <input
                        type="radio"
                        formControlName="vatTreatment"
                        [value]="treatment.value"
                      />
                      <span class="choice__text">
                        <span class="choice__label">{{ treatment.label }}</span>
                        <span class="choice__hint">{{ treatment.hint }}</span>
                      </span>
                    </label>
                  }
                </div>
              </app-field-group>
              <app-form-field
                label="Date"
                [control]="form.controls.entryDate"
                requiredMessage="Choose a date."
              >
                <input appControl type="date" formControlName="entryDate" />
              </app-form-field>
            } @else {
              <app-skeleton [lines]="4" label="Loading the form" />
            }
          } @else {
            <app-field-group
              label="Budget line"
              [control]="form.controls.budgetLineId"
              requiredMessage="Choose the budget line to change."
            >
              <app-search-box
                label="Search budget lines"
                placeholder="Search budget lines"
                (searched)="search($event)"
              />
              @if (pickerLoading()) {
                <app-skeleton [lines]="3" label="Loading budget lines" />
              } @else if (choices().length === 0) {
                <p class="text-muted text-small">
                  No active budget line matches. Try another search.
                </p>
              } @else {
                <div class="choice-list picker">
                  @for (choice of choices(); track choice.id) {
                    <label class="choice">
                      <input
                        type="radio"
                        formControlName="budgetLineId"
                        [value]="choice.id"
                        (change)="picked(choice)"
                      />
                      <span class="choice__text">
                        <span class="choice__label">{{ choice.description }}</span>
                        <span class="choice__hint">{{ describe(choice) }}</span>
                      </span>
                    </label>
                  }
                </div>
                @if (moreThanShown()) {
                  <p class="text-muted text-small">
                    Showing the first {{ pickerSize }}. Search to find another line.
                  </p>
                }
              }
            </app-field-group>

            @if (kind() === 'Revise' && chosen(); as line) {
              <p class="now text-small text-muted">Now: {{ describe(line) }}</p>
              <div class="form__row">
                <app-form-field
                  label="New quantity"
                  [control]="form.controls.quantity"
                  requiredMessage="Enter the quantity."
                >
                  <app-number-input formControlName="quantity" [decimals]="4" placeholder="0" />
                </app-form-field>
                <app-form-field
                  label="New unit cost"
                  [control]="form.controls.unitCostMinor"
                  requiredMessage="Enter the cost of one unit."
                >
                  <app-money-input formControlName="unitCostMinor" placeholder="0.00" />
                </app-form-field>
              </div>
              <p class="text-muted text-small">
                Its VAT treatment, rate and description stay as they are.
              </p>
            }
          }
        </form>
      </div>

      <div dialogActions class="dialog-actions">
        <button
          appButton
          type="button"
          [attr.disabled]="saving() ? '' : null"
          (click)="dialog().dismiss()"
        >
          Cancel
        </button>
        <button appButton variant="primary" type="submit" form="co-line-form" [busy]="saving()">
          {{ line() ? 'Save line' : 'Add line' }}
        </button>
      </div>
    </app-dialog>
  `,
})
export class ChangeOrderLineDialog implements OnInit {
  readonly order = input.required<ChangeOrder>();
  /** The line to edit, or null to add one. */
  readonly line = input<ChangeOrderLine | null>(null);

  readonly saved = output<ChangeOrder>();
  readonly dismissed = output<void>();
  readonly reloadRequested = output<void>();

  protected readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly api = inject(ChangeOrdersApi);
  private readonly costing = inject(CostingApi);
  private readonly context = inject(ProjectContext);
  private readonly session = inject(SessionState);
  protected readonly referenceState = inject(CostingReferenceState);
  private readonly focusInvalid = injectFocusFirstInvalid();

  protected readonly kinds = KINDS;
  protected readonly pickerSize = PICKER_SIZE;
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);
  protected readonly pickerLoading = signal(false);
  protected readonly choices = signal<readonly BudgetLine[]>([]);
  protected readonly moreThanShown = signal(false);
  protected readonly chosen = signal<BudgetLine | null>(null);
  protected readonly subProjects = this.context.activeSubProjects;

  protected readonly form = new FormGroup({
    kind: new FormControl<ChangeOrderLineKind>('Add', { nonNullable: true }),
    budgetLineId: new FormControl('', { nonNullable: true }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(200)],
    }),
    itemType: new FormControl('', { nonNullable: true }),
    division: new FormControl('', { nonNullable: true }),
    wbsCode: new FormControl('', { nonNullable: true }),
    subProjectId: new FormControl('', { nonNullable: true }),
    quantity: new FormControl<number | null>(null),
    unit: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(20)] }),
    unitCostMinor: new FormControl<number | null>(null),
    vatTreatment: new FormControl('', { nonNullable: true }),
    entryDate: new FormControl(todayIn(this.session.timeZoneId()), { nonNullable: true }),
  });

  protected readonly kind = toSignal(this.form.controls.kind.valueChanges, {
    initialValue: this.form.controls.kind.value,
  });
  private readonly division = toSignal(this.form.controls.division.valueChanges, {
    initialValue: '',
  });
  protected readonly scopes = computed(() => {
    const division = this.division();
    return (
      this.referenceState.divisions().find((item) => String(item.number) === division)?.scopes ?? []
    );
  });

  private searchText = '';

  ngOnInit(): void {
    void this.referenceState.load();
    const line = this.line();
    if (line) {
      this.form.patchValue({
        kind: line.kind,
        budgetLineId: line.budgetLineId ?? '',
        description: line.kind === 'Add' ? line.description : '',
        itemType: line.kind === 'Add' ? line.itemType : '',
        division: line.kind === 'Add' && line.division !== null ? String(line.division) : '',
        wbsCode: line.kind === 'Add' ? (line.wbsCode ?? '') : '',
        subProjectId: line.kind === 'Add' ? (line.subProjectId ?? '') : '',
        quantity: line.kind === 'Omit' ? null : line.quantity,
        unit: line.kind === 'Add' ? line.unit : '',
        unitCostMinor: line.kind === 'Omit' ? null : line.unitCostMinor,
        vatTreatment: line.kind === 'Add' ? line.vatTreatment : '',
        entryDate: line.kind === 'Add' ? line.entryDate : this.form.controls.entryDate.value,
      });
    }
    this.form.controls.kind.valueChanges.subscribe((kind) => {
      if (kind !== 'Add' && this.choices().length === 0) void this.loadChoices();
    });
    this.form.controls.division.valueChanges.subscribe(() =>
      this.form.controls.wbsCode.setValue(''),
    );
    if (this.form.controls.kind.value !== 'Add') void this.loadChoices();
  }

  protected describe(line: BudgetLine): string {
    return describeBudgetLine(line, this.session.currencyCode());
  }

  protected search(text: string): void {
    this.searchText = text;
    void this.loadChoices();
  }

  protected picked(line: BudgetLine): void {
    this.chosen.set(line);
    if (this.form.controls.kind.value === 'Revise')
      this.form.patchValue({ quantity: line.quantity, unitCostMinor: line.unitCostMinor });
  }

  private async loadChoices(): Promise<void> {
    this.pickerLoading.set(true);
    try {
      // Lines this order already changes can't be chosen twice (the line being edited stays available).
      const taken = new Set(
        this.order()
          .lines.filter((item) => item.kind !== 'Add' && item.id !== this.line()?.id)
          .map((item) => item.budgetLineId),
      );
      const page = await firstValueFrom(
        this.costing.listBudgetLines(
          this.context.project().id,
          {
            subProjectId: '',
            division: 0,
            itemType: '',
            search: this.searchText,
            includeRemoved: false,
          },
          { page: 1, pageSize: PICKER_SIZE },
        ),
      );
      this.choices.set(page.items.filter((item) => !taken.has(item.id)));
      this.moreThanShown.set(page.total > page.items.length);
      const current = this.form.controls.budgetLineId.value;
      const match = page.items.find((item) => item.id === current);
      if (match) this.chosen.set(match);
    } catch (failure) {
      this.error.set(asApiError(failure));
    } finally {
      this.pickerLoading.set(false);
    }
  }

  private fields(): ChangeOrderLineFields | null {
    const value = this.form.getRawValue();
    const required = (control: AbstractControl, empty: boolean) => {
      if (empty) control.setErrors({ required: true });
    };
    if (value.kind === 'Add') {
      required(this.form.controls.description, value.description.trim() === '');
      required(this.form.controls.itemType, value.itemType === '');
      required(this.form.controls.quantity, value.quantity === null);
      required(this.form.controls.unit, value.unit.trim() === '');
      required(this.form.controls.unitCostMinor, value.unitCostMinor === null);
      required(this.form.controls.vatTreatment, value.vatTreatment === '');
      required(this.form.controls.wbsCode, value.division !== '' && value.wbsCode === '');
      if (this.form.invalid) return null;
      const division = value.division === '' ? null : Number(value.division);
      const owner = this.referenceState.divisions().find((item) => item.number === division);
      const scope = owner?.scopes.find((item) => item.code === value.wbsCode);
      return {
        kind: 'Add',
        subProjectId: value.subProjectId === '' ? null : value.subProjectId,
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
        entryDate: value.entryDate,
      };
    }
    required(this.form.controls.budgetLineId, value.budgetLineId === '');
    if (value.kind === 'Revise') {
      required(this.form.controls.quantity, value.quantity === null);
      required(this.form.controls.unitCostMinor, value.unitCostMinor === null);
    }
    if (this.form.invalid) return null;
    return value.kind === 'Revise'
      ? {
          kind: 'Revise',
          budgetLineId: value.budgetLineId,
          quantity: value.quantity ?? 0,
          unitCostMinor: value.unitCostMinor ?? 0,
        }
      : { kind: 'Omit', budgetLineId: value.budgetLineId };
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    const fields = this.fields();
    if (!fields) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    try {
      const order = this.order();
      const line = this.line();
      const result = await firstValueFrom(
        line
          ? this.api.updateLine(order.id, line.id, fields, order.version)
          : this.api.addLine(order.id, fields, order.version),
      );
      this.saved.emit(result);
    } catch (failure) {
      const problem = asApiError(failure);
      const unmatched = applyServerErrors(this.form, problem);
      this.extra.set(unmatched);
      if (!problem.isValidation || unmatched.length > 0) this.error.set(problem);
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }
}
