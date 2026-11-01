import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom, startWith } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { SessionState } from '../../core/session/session-state';
import { applyServerErrors, injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { todayIn } from '../../shared/presentation/business-time';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Control } from '../../shared/ui/form-field/control';
import { FieldGroup } from '../../shared/ui/form-field/field-group';
import { FormField } from '../../shared/ui/form-field/form-field';
import { MoneyInput } from '../../shared/ui/inputs/money-input';
import { NumberInput } from '../../shared/ui/inputs/number-input';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { InventoryApi } from './inventory-api';
import {
  CATEGORIES,
  CATEGORY,
  CATEGORY_EXPLANATION,
  categoryRules,
} from './inventory-presentation';
import {
  InventoryCategory,
  InventoryItem,
  ItemDetails,
  RegisterItemRequest,
  UNIT_SUGGESTIONS,
} from './inventory.models';
import { RequestId } from './request-id';
import { dateNotAfter, greaterThanZero, notBlank } from './stock-validators';

/** The form names the unit cost and the supplier the way the person does; the API has its own names for them. */
const SERVER_FIELD_NAMES = { unitCostMinor: 'unitCost', sourceDealer: 'supplier' } as const;

/**
 * Registers an item (`/stock/new`) or edits its details (`/stock/:id/edit`). Equipment and machinery are single pieces: choosing
 * one makes the serial number required and fixes the quantity at 1, so the quantity box goes away; a tool asks how many. What
 * the server still refuses (a serial number already in use, a future date) is shown under the right field. A register carries
 * one request id for the whole form session, so a double press or a retry returns the first result instead of a second item.
 */
@Component({
  selector: 'app-item-form-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    PageHeader,
    FormField,
    FieldGroup,
    Control,
    MoneyInput,
    NumberInput,
    Button,
    Alert,
    ApiErrorAlert,
    Skeleton,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './item-form-page.html',
})
export class ItemFormPage {
  /** Bound from the route: set when editing, absent when registering. */
  readonly id = input<string | undefined>(undefined);

  private readonly api = inject(InventoryApi);
  private readonly session = inject(SessionState);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly todayDate = todayIn(this.session.timeZoneId());

  protected readonly categories = CATEGORIES.map((value) => ({ value, ...CATEGORY[value] }));
  protected readonly categoryExplanation = CATEGORY_EXPLANATION;
  protected readonly units = UNIT_SUGGESTIONS;

  protected readonly form = new FormGroup({
    category: new FormControl<InventoryCategory | null>(null, [Validators.required]),
    description: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, notBlank, Validators.maxLength(200)],
    }),
    brand: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(80)] }),
    serialNumber: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(80)],
    }),
    quantity: new FormControl<number | null>(null),
    unit: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, notBlank, Validators.maxLength(20)],
    }),
    supplier: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(120)] }),
    invoiceNumber: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(60)],
    }),
    purchaseDate: new FormControl(this.todayDate, {
      nonNullable: true,
      validators: [
        Validators.required,
        dateNotAfter(this.todayDate, "The purchase date can't be in the future."),
      ],
    }),
    unitCost: new FormControl<number | null>(null, [Validators.min(0)]),
    remarks: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(500)] }),
  });

  protected readonly isEdit = computed(() => this.id() !== undefined);
  protected readonly loadingItem = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extraMessages = signal<readonly string[]>([]);
  protected readonly showSummary = signal(false);

  /** The item being edited, as last loaded. Its version goes back with the save. */
  protected readonly loaded = signal<InventoryItem | null>(null);

  private readonly category = toSignal(
    this.form.controls.category.valueChanges.pipe(startWith(this.form.controls.category.value)),
    { initialValue: null },
  );

  /** What the chosen category means for the form: a single piece needs a serial number and has no quantity to type. */
  protected readonly rules = computed(() => categoryRules(this.category()));
  protected readonly categoryChosen = computed(() => this.category() !== null);
  protected readonly categoryLabel = computed(() => {
    const category = this.category();
    return category ? CATEGORY[category].label : '';
  });

  /** A tool being edited may already carry a serial number; keep it visible and editable. */
  protected readonly showSerial = computed(
    () => this.rules().singlePiece || (this.isEdit() && !!this.loaded()?.serialNumber),
  );

  protected readonly backLink = computed(() => (this.id() ? ['/stock', this.id()] : ['/stock']));
  protected readonly backLabel = computed(() => (this.id() ? 'Back to item' : 'All stock'));

  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();
  private readonly requestId = new RequestId();

  constructor() {
    this.form.controls.category.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((category) => this.applyCategory(category));

    effect(() => {
      const id = this.id();
      if (id) untracked(() => void this.load(id));
    });
  }

  /** Switches the rules that depend on the category: the serial number is required for a single piece, the quantity for a tool. */
  private applyCategory(category: InventoryCategory | null): void {
    const rules = categoryRules(category);
    const { serialNumber, quantity, unit } = this.form.controls;
    serialNumber.setValidators(
      rules.serialRequired
        ? [Validators.required, notBlank, Validators.maxLength(80)]
        : [Validators.maxLength(80)],
    );
    // The quantity is only asked when registering a tool: an edit changes details, and stock changes have their own actions.
    quantity.setValidators(
      !this.isEdit() && category !== null && !rules.singlePiece
        ? [Validators.required, greaterThanZero]
        : [],
    );
    serialNumber.updateValueAndValidity({ emitEvent: false });
    quantity.updateValueAndValidity({ emitEvent: false });
    // A starting suggestion, only while the person has not typed a unit of their own.
    if (!this.isEdit() && unit.pristine && unit.value === '' && category !== null)
      unit.setValue(rules.singlePiece ? 'unit' : 'pc');
  }

  /** Fetches the item again and replaces what is in the form (after a conflict, or on first load). */
  protected async load(id: string): Promise<void> {
    this.loadingItem.set(true);
    this.error.set(null);
    try {
      const item = await firstValueFrom(this.api.get(id));
      this.loaded.set(item);
      this.form.controls.category.setValue(item.category);
      this.form.reset({
        category: item.category,
        description: item.description,
        brand: item.brand ?? '',
        serialNumber: item.serialNumber ?? '',
        quantity: null,
        unit: item.unit,
        supplier: item.sourceDealer ?? '',
        invoiceNumber: item.invoiceNumber ?? '',
        purchaseDate: item.purchaseDate,
        unitCost: item.unitCostMinor > 0 ? item.unitCostMinor : null,
        remarks: item.remarks ?? '',
      });
      this.applyCategory(item.category);
    } catch (failure) {
      this.error.set(asApiError(failure));
    } finally {
      this.loadingItem.set(false);
    }
  }

  protected reload(): void {
    const id = this.id();
    if (id) void this.load(id);
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.showSummary.set(true);
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }

    this.saving.set(true);
    this.showSummary.set(false);
    this.error.set(null);
    this.extraMessages.set([]);
    const id = this.id();
    try {
      const saved = id
        ? await firstValueFrom(
            this.api.update(id, { ...this.details(), version: this.loaded()?.version ?? 0 }),
          )
        : await firstValueFrom(this.api.register(this.registration()));
      this.requestId.renew();
      this.toast.success(id ? 'Details saved.' : 'Item registered.');
      await this.router.navigate(['/stock', saved.id]);
    } catch (failure) {
      const problem = asApiError(failure);
      const unmatched = applyServerErrors(this.form, problem, SERVER_FIELD_NAMES);
      // A message under a field is enough for field problems; anything else goes above the form.
      if (
        problem.isConflict ||
        problem.isRuleViolation ||
        problem.isServerFailure ||
        unmatched.length > 0 ||
        !problem.isValidation
      )
        this.error.set(problem);
      this.extraMessages.set(unmatched);
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }

  private details(): ItemDetails {
    const value = this.form.getRawValue();
    return {
      description: value.description.trim(),
      brand: value.brand.trim() || null,
      serialNumber: this.showSerial() ? value.serialNumber.trim() || null : null,
      unit: value.unit.trim(),
      sourceDealer: value.supplier.trim() || null,
      invoiceNumber: value.invoiceNumber.trim() || null,
      purchaseDate: value.purchaseDate,
      unitCostMinor: value.unitCost ?? 0,
      remarks: value.remarks.trim() || null,
    };
  }

  private registration(): RegisterItemRequest {
    const rules = this.rules();
    return {
      ...this.details(),
      category: this.form.controls.category.value as InventoryCategory,
      // A single piece is always one; a tool takes what the person typed.
      quantity: rules.fixedQuantity ?? this.form.controls.quantity.value ?? 0,
      clientRequestId: this.requestId.current(),
    };
  }
}
