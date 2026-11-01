import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  OnInit,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { Observable, firstValueFrom, merge, startWith } from 'rxjs';
import { ApiError, asApiError } from '../../../core/api/api-error';
import { ToastService } from '../../../core/notifications/toast-service';
import { SessionState } from '../../../core/session/session-state';
import { applyServerErrors, injectFocusFirstInvalid } from '../../../shared/forms/server-errors';
import { todayIn } from '../../../shared/presentation/business-time';
import { formatMoney } from '../../../shared/presentation/money';
import { formatBps } from '../../../shared/presentation/percent';
import { Alert } from '../../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../../shared/ui/button/button';
import { Dialog } from '../../../shared/ui/dialog/dialog';
import { Control } from '../../../shared/ui/form-field/control';
import { FieldGroup } from '../../../shared/ui/form-field/field-group';
import { FormField } from '../../../shared/ui/form-field/form-field';
import { MoneyInput } from '../../../shared/ui/inputs/money-input';
import { NumberInput } from '../../../shared/ui/inputs/number-input';
import { Skeleton } from '../../../shared/ui/skeleton/skeleton';
import { ProjectContext } from '../../projects/project-context';
import { ClientRequestId } from '../client-request-id';
import { CostingApi } from '../costing-api';
import { CostingReferenceState } from '../costing-reference';
import { BudgetLine, CostEntry, CostEntryFields, LineFields } from '../costing.models';
import { lineTotalMinor } from '../line-amount';
import {
  CUSTOM_CODE,
  LineForm,
  LineKind,
  buildLineFields,
  createLineForm,
  duplicateInvoiceDate,
  formValueFromLine,
  isDuplicateInvoiceMessage,
  resetClassification,
  scopesOf,
  routeServerErrors,
} from './line-form';

let nextDialogId = 0;

/** What is known about the invoice the server says is already recorded, for finding the earlier entry. */
export interface ExistingInvoice {
  readonly supplier: string;
  readonly invoiceNumber: string;
  /** The day the earlier entry is dated ("2026-12-03"), when the server's message gave one. */
  readonly date: string | null;
}

/**
 * Adds or edits a budget line or a manual cost entry: one form, because the two differ only in the supplier and invoice a cost
 * needs. The money preview ("Line total") is a quick check; every saved figure comes back from the server.
 *
 * Every attempt to save one new record carries the same `clientRequestId`, so a double tap or a retry cannot record it twice; a
 * successful save renews it. An edit sends the version it loaded, and a conflict offers to reload the latest.
 */
@Component({
  selector: 'app-line-dialog',
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
  templateUrl: './line-dialog.html',
  styleUrl: './line-dialog.scss',
})
export class LineDialog implements OnInit {
  readonly kind = input.required<LineKind>();
  /** The record to edit; leave it out to add a new one. */
  readonly line = input<BudgetLine | CostEntry | null>(null);

  /** The record was saved: the page refreshes its list and the dialog closes. */
  readonly saved = output<BudgetLine | CostEntry>();
  /** The dialog was dismissed or finished: the page removes it. */
  readonly dismissed = output<void>();
  /** After a conflict the latest version was fetched: the list behind the dialog is stale too. */
  readonly refreshed = output<void>();
  /** The person wants to see the entry that already holds this invoice. */
  readonly findExisting = output<ExistingInvoice>();

  private readonly api = inject(CostingApi);
  private readonly context = inject(ProjectContext);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  protected readonly referenceState = inject(CostingReferenceState);

  private readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  protected form!: LineForm;
  protected readonly requestId = new ClientRequestId();
  protected readonly ids = { units: `line-units-${++nextDialogId}` };

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extraMessages = signal<readonly string[]>([]);
  protected readonly showSummary = signal(false);
  /** Set when the server says this supplier invoice is already recorded: the date of the earlier entry, if it named one. */
  protected readonly duplicate = signal<{ readonly date: string | null } | null>(null);
  protected readonly previewMinor = signal<number | null>(null);
  protected readonly division = signal('');
  protected readonly wbsChoice = signal('');

  /** The record as last loaded: its version goes back with an edit. */
  private readonly loaded = signal<BudgetLine | CostEntry | null>(null);
  private needsFilling = false;

  protected readonly reference = this.referenceState.reference;
  protected readonly isEdit = computed(() => this.line() !== null);
  protected readonly isCost = computed(() => this.kind() === 'cost');
  protected readonly today = todayIn(this.session.timeZoneId());

  protected readonly title = computed(() =>
    this.isCost()
      ? this.isEdit()
        ? 'Edit cost'
        : 'Record a cost'
      : this.isEdit()
        ? 'Edit budget line'
        : 'Add budget line',
  );
  protected readonly saveLabel = computed(() =>
    this.isCost()
      ? this.isEdit()
        ? 'Save cost'
        : 'Record cost'
      : this.isEdit()
        ? 'Save budget line'
        : 'Add budget line',
  );
  protected readonly noun = computed(() => (this.isCost() ? 'cost' : 'budget line'));

  protected readonly subProjectOptions = computed(() => {
    const keep = this.loaded()?.subProjectId ?? null;
    return this.context.subProjects().filter((sub) => sub.status === 'Active' || sub.id === keep);
  });

  protected readonly scopes = computed(() =>
    scopesOf(this.reference()?.divisions ?? [], this.division()),
  );
  protected readonly customCode = computed(() => this.wbsChoice() === CUSTOM_CODE);
  protected readonly customChoice = CUSTOM_CODE;

  protected readonly previewText = computed(() => {
    const minor = this.previewMinor();
    return minor === null ? null : formatMoney(minor, this.session.currencyCode());
  });

  protected readonly vatHint = computed(() => {
    const own = this.loaded();
    return own
      ? `This ${this.noun()} keeps the ${formatBps(own.vatRateBps)} VAT rate it was entered with.`
      : `VAT is worked out at the company rate of ${formatBps(this.session.vatRateBps())}.`;
  });

  constructor() {
    void this.referenceState.load();

    // The form is filled once the choices it needs (the division's codes) have arrived.
    effect(() => {
      const reference = this.reference();
      if (reference && this.needsFilling) untracked(() => this.fill());
    });
  }

  ngOnInit(): void {
    this.form = createLineForm(this.kind(), this.today);

    const controls = this.form.controls;
    this.form.valueChanges
      .pipe(startWith(null), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        const value = this.form.getRawValue();
        this.previewMinor.set(lineTotalMinor(value.quantity, value.unitCostMinor));
        this.division.set(value.division);
        this.wbsChoice.set(value.wbsChoice);
      });
    // A new invoice number or supplier is a new question: the earlier "already recorded" answer no longer applies.
    merge(controls.invoiceNumber.valueChanges, controls.sourceDealer.valueChanges)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.duplicate.set(null));

    const existing = this.line();
    if (existing) {
      this.loaded.set(existing);
      this.needsFilling = true;
      if (this.reference()) this.fill();
    }
  }

  protected cancel(): void {
    this.dialog().dismiss();
  }

  protected divisionChanged(): void {
    resetClassification(this.form);
  }

  protected retryReference(): void {
    void this.referenceState.load();
  }

  protected openExisting(): void {
    this.findExisting.emit({
      supplier: this.form.controls.sourceDealer.value.trim(),
      invoiceNumber: this.form.controls.invoiceNumber.value.trim(),
      date: this.duplicate()?.date ?? null,
    });
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    const reference = this.reference();
    if (!reference) return;

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
    const fields = buildLineFields(this.kind(), this.form.getRawValue(), reference.divisions);
    try {
      const saved = await firstValueFrom(this.send(fields));
      // The next record saved from this form is a new request.
      this.requestId.renew();
      this.toast.success(this.doneMessage());
      this.saved.emit(saved);
    } catch (failure) {
      this.showFailure(asApiError(failure));
    } finally {
      this.saving.set(false);
    }
  }

  /** After a conflict: fetch the latest version of the record and show it, keeping the dialog open. */
  protected async reload(): Promise<void> {
    const current = this.loaded();
    if (!current) return;
    this.error.set(null);
    try {
      const fetch: Observable<BudgetLine | CostEntry> = this.isCost()
        ? this.api.getCostEntry(current.id)
        : this.api.getBudgetLine(current.id);
      const latest = await firstValueFrom(fetch);
      this.loaded.set(latest);
      this.fill();
      this.refreshed.emit();
    } catch (failure) {
      this.error.set(asApiError(failure));
    }
  }

  private fill(): void {
    const reference = this.reference();
    const record = this.loaded();
    if (!reference || !record) return;
    this.needsFilling = false;
    this.form.reset(formValueFromLine(record, reference.divisions));
    this.showSummary.set(false);
    this.duplicate.set(null);
  }

  private send(fields: LineFields | CostEntryFields): Observable<BudgetLine | CostEntry> {
    const projectId = this.context.project().id;
    const current = this.loaded();
    if (this.isCost()) {
      const cost = fields as CostEntryFields;
      return current
        ? this.api.updateCostEntry(current.id, cost, current.version)
        : this.api.createCostEntry(projectId, cost, this.requestId.current);
    }
    return current
      ? this.api.updateBudgetLine(current.id, fields, current.version)
      : this.api.createBudgetLine(projectId, fields, this.requestId.current);
  }

  private showFailure(problem: ApiError): void {
    const value = this.form.getRawValue();
    const unmatched = applyServerErrors(this.form, {
      errors: routeServerErrors(problem.errors, value),
    });
    const invoiceMessage = problem.firstError('invoiceNumber');
    this.duplicate.set(
      this.isCost() && isDuplicateInvoiceMessage(invoiceMessage)
        ? { date: duplicateInvoiceDate(invoiceMessage) }
        : null,
    );
    // A message already under its field is not repeated above the form; anything else (a conflict, a rule, a failure) is.
    if (
      problem.isConflict ||
      problem.isRuleViolation ||
      problem.isServerFailure ||
      !problem.isValidation ||
      unmatched.length > 0
    ) {
      this.error.set(problem);
      this.extraMessages.set(unmatched);
    }
    this.reveal();
  }

  /**
   * Shows the person what went wrong. A message above the form (a conflict, a rule, a failure) is at the top of the scrolling
   * body, which may be scrolled far down: bring it into view. Otherwise the keyboard goes to the first field that needs attention.
   */
  private reveal(): void {
    const form = this.formElement()?.nativeElement;
    if (this.error() === null) {
      this.focusInvalid(form);
      return;
    }
    afterNextRender(
      () => form?.closest('.dialog__body')?.scrollTo?.({ top: 0, behavior: 'auto' }),
      { injector: this.injector },
    );
  }

  private doneMessage(): string {
    const edit = this.isEdit();
    if (this.isCost()) return edit ? 'Cost saved.' : 'Cost recorded.';
    return edit ? 'Budget line saved.' : 'Budget line added.';
  }
}
