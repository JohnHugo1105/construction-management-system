import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, firstValueFrom, of, startWith } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { SessionState } from '../../core/session/session-state';
import { applyServerErrors, injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { todayIn } from '../../shared/presentation/business-time';
import { formatMoney } from '../../shared/presentation/money';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { Icon } from '../../shared/ui/icon/icon';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { ClientRequestId } from '../costing/client-request-id';
import { OrderLineDialog } from './order-line-dialog';
import { PurchasingApi } from './purchasing-api';
import {
  ProjectChoice,
  PurchaseOrder,
  PurchaseOrderFields,
  PurchaseOrderLineFields,
  SupplierLookup,
} from './purchasing.models';

/**
 * Drafts a purchase order (`/purchasing/orders/new`, optionally `?project=<id>`) or changes a draft
 * (`/purchasing/orders/:id/edit`): the supplier, the project, the dates, and the lines, each added in a dialog. Only a supplier firm
 * can be chosen. The saved total is worked out by the server when the order is saved and shown on the order page. A new order
 * carries one request id for the whole form session, so a double press or a retry returns the first order instead of a second.
 */
@Component({
  selector: 'app-order-form-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    PageHeader,
    FormField,
    Control,
    Button,
    Icon,
    Alert,
    ApiErrorAlert,
    Skeleton,
    OrderLineDialog,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './order-form-page.html',
  styleUrl: './order-form-page.scss',
})
export class OrderFormPage {
  /** Bound from the route: set when changing a draft, absent when raising a new order. */
  readonly id = input<string | undefined>(undefined);

  private readonly api = inject(PurchasingApi);
  private readonly session = inject(SessionState);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly toast = inject(ToastService);
  protected readonly todayDate = todayIn(this.session.timeZoneId());
  protected readonly currency = this.session.currencyCode;

  protected readonly form = new FormGroup({
    supplierId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    projectId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    subProjectId: new FormControl('', { nonNullable: true }),
    orderDate: new FormControl(this.todayDate, { nonNullable: true, validators: [Validators.required] }),
    expectedDate: new FormControl('', { nonNullable: true }),
    supplierReference: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(80)] }),
    notes: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(500)] }),
  });

  private readonly suppliers = toSignal(
    this.api.supplierLookup('Supplier').pipe(catchError(() => of([] as readonly SupplierLookup[]))),
    { initialValue: null },
  );
  private readonly projects = toSignal(
    this.api.projectLookup(false).pipe(catchError(() => of([] as readonly ProjectChoice[]))),
    { initialValue: null },
  );

  protected readonly loaded = signal<PurchaseOrder | null>(null);
  protected readonly loadingOrder = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extraMessages = signal<readonly string[]>([]);
  protected readonly showSummary = signal(false);
  protected readonly lines = signal<readonly PurchaseOrderLineFields[]>([]);
  /** The dialog is open: a number is the line being changed, 'new' adds one. */
  protected readonly editing = signal<number | 'new' | null>(null);
  protected readonly linesProblem = signal<string | null>(null);

  protected readonly isEdit = computed(() => this.id() !== undefined);
  protected readonly editingLine = computed(() => {
    const index = this.editing();
    return typeof index === 'number' ? (this.lines()[index] ?? null) : null;
  });

  private readonly chosenProject = toSignal(
    this.form.controls.projectId.valueChanges.pipe(startWith(this.form.controls.projectId.value)),
    { initialValue: '' },
  );

  protected readonly supplierOptions = computed(() => {
    const list = this.suppliers() ?? [];
    const own = this.loaded();
    // A draft's firm may have been archived since: keep it visible so the form can still be read, the server says what to do.
    return own && !list.some((item) => item.id === own.supplierId)
      ? [...list, { id: own.supplierId, name: `${own.supplierName} (archived)`, isSupplier: true, isSubcontractor: false }]
      : list;
  });
  protected readonly projectOptions = computed(() => this.projects() ?? []);
  protected readonly subProjectOptions = computed(() => {
    const project = this.projectOptions().find((item) => item.id === this.chosenProject());
    const keep = this.loaded()?.subProjectId ?? null;
    return (project?.subProjects ?? []).filter((sub) => sub.status === 'Active' || sub.id === keep);
  });
  protected readonly pickersLoading = computed(() => this.suppliers() === null || this.projects() === null);
  protected readonly noSuppliers = computed(() => this.suppliers() !== null && this.suppliers()!.length === 0);

  protected readonly lineRows = computed(() =>
    this.lines().map((line, index) => ({
      index,
      description: line.description,
      detail: `${line.quantity} ${line.unit} at ${formatMoney(line.unitCostMinor, this.currency())}`,
    })),
  );
  protected readonly savedTotal = computed(() => {
    const order = this.loaded();
    return order ? formatMoney(order.grossMinor, this.currency()) : null;
  });

  protected readonly backLink = computed(() =>
    this.id() ? ['/purchasing/orders', this.id()] : ['/purchasing'],
  );
  protected readonly backLabel = computed(() => (this.id() ? 'Back to order' : 'All orders'));

  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();
  private readonly requestId = new ClientRequestId();

  constructor() {
    const project = this.route.snapshot.queryParamMap.get('project');
    if (project) this.form.controls.projectId.setValue(project);

    effect(() => {
      const id = this.id();
      if (id) untracked(() => void this.load(id));
    });
  }

  protected async load(id: string): Promise<void> {
    this.loadingOrder.set(true);
    this.error.set(null);
    try {
      const order = await firstValueFrom(this.api.getOrder(id));
      this.loaded.set(order);
      this.fill(order);
    } catch (failure) {
      this.error.set(asApiError(failure));
    } finally {
      this.loadingOrder.set(false);
    }
  }

  private fill(order: PurchaseOrder): void {
    this.form.reset({
      supplierId: order.supplierId,
      projectId: order.projectId,
      subProjectId: order.subProjectId ?? '',
      orderDate: order.orderDate,
      expectedDate: order.expectedDate ?? '',
      supplierReference: order.supplierReference ?? '',
      notes: order.notes ?? '',
    });
    this.lines.set(
      order.lines.map((line) => ({
        division: line.division,
        mainScope: line.mainScope,
        subScope: line.subScope,
        wbsCode: line.wbsCode,
        description: line.description,
        itemType: line.itemType,
        quantity: line.quantity,
        unit: line.unit,
        unitCostMinor: line.unitCostMinor,
        vatTreatment: line.vatTreatment,
      })),
    );
    this.showSummary.set(false);
  }

  protected projectChanged(): void {
    this.form.controls.subProjectId.setValue('');
  }

  protected addLine(): void {
    this.editing.set('new');
  }

  protected changeLine(index: number): void {
    this.editing.set(index);
  }

  protected removeLine(index: number): void {
    this.lines.update((lines) => lines.filter((_, position) => position !== index));
  }

  protected lineSaved(line: PurchaseOrderLineFields): void {
    const index = this.editing();
    this.lines.update((lines) =>
      typeof index === 'number' ? lines.map((item, position) => (position === index ? line : item)) : [...lines, line],
    );
    this.linesProblem.set(null);
    this.editing.set(null);
  }

  protected closeLineDialog(): void {
    this.editing.set(null);
  }

  protected async reload(): Promise<void> {
    const id = this.id();
    if (id) await this.load(id);
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    this.linesProblem.set(this.lines().length === 0 ? 'Add at least one line to the order.' : null);
    if (this.form.invalid || this.lines().length === 0) {
      this.showSummary.set(true);
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }

    const value = this.form.getRawValue();
    const fields: PurchaseOrderFields = {
      projectId: value.projectId,
      subProjectId: value.subProjectId === '' ? null : value.subProjectId,
      supplierId: value.supplierId,
      orderDate: value.orderDate,
      expectedDate: value.expectedDate === '' ? null : value.expectedDate,
      supplierReference: value.supplierReference.trim() === '' ? null : value.supplierReference.trim(),
      notes: value.notes.trim() === '' ? null : value.notes.trim(),
      lines: this.lines(),
    };

    this.saving.set(true);
    this.showSummary.set(false);
    this.error.set(null);
    this.extraMessages.set([]);
    try {
      const current = this.loaded();
      const saved = await firstValueFrom(
        current
          ? this.api.updateOrder(current.id, fields, current.version)
          : this.api.createOrder(fields, this.requestId.current),
      );
      this.requestId.renew();
      this.toast.success(current ? 'Draft saved.' : `Purchase order ${saved.number} drafted.`);
      await this.router.navigate(['/purchasing/orders', saved.id]);
    } catch (failure) {
      this.showFailure(asApiError(failure));
    } finally {
      this.saving.set(false);
    }
  }

  private showFailure(problem: ApiError): void {
    const unmatched = applyServerErrors(this.form, problem);
    const lineMessages = problem.errors['lines'] ?? [];
    if (lineMessages.length > 0) this.linesProblem.set(lineMessages.join(' '));
    const outside = unmatched.filter((message) => !lineMessages.includes(message));
    if (problem.isConflict || problem.isRuleViolation || problem.isServerFailure || !problem.isValidation || outside.length > 0) {
      this.error.set(problem);
      this.extraMessages.set(outside);
    }
    this.focusInvalid(this.formElement()?.nativeElement);
  }
}
