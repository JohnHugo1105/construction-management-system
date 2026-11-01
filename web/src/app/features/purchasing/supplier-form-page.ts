import { ChangeDetectionStrategy, Component, ElementRef, computed, effect, inject, input, signal, untracked, viewChild } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { applyServerErrors, injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { ConfirmDialog } from '../../shared/ui/dialog/confirm-dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { PurchasingApi } from './purchasing-api';
import { Supplier, SupplierFields } from './purchasing.models';

/**
 * Adds a firm (`/purchasing/suppliers/new`) or shows and changes one (`/purchasing/suppliers/:id`). A firm is a supplier, a
 * subcontractor, or both. People who may not manage firms see the details and no buttons that change them. Archiving keeps the
 * firm on every order and invoice; a firm can be restored.
 */
@Component({
  selector: 'app-supplier-form-page',
  imports: [ReactiveFormsModule, RouterLink, PageHeader, FormField, Control, Button, Alert, ApiErrorAlert, Skeleton, ConfirmDialog],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './supplier-form-page.html',
  styleUrl: './supplier-form-page.scss',
})
export class SupplierFormPage {
  /** Bound from the route: set when opening a firm, absent when adding one. */
  readonly id = input<string | undefined>(undefined);

  private readonly api = inject(PurchasingApi);
  private readonly session = inject(SessionState);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(120)] }),
    isSupplier: new FormControl(true, { nonNullable: true }),
    isSubcontractor: new FormControl(false, { nonNullable: true }),
    tin: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(30)] }),
    contactPerson: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(120)] }),
    phone: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(40)] }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(254), Validators.email] }),
    address: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(250)] }),
    notes: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(500)] }),
  });

  protected readonly loaded = signal<Supplier | null>(null);
  protected readonly loadingFirm = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extraMessages = signal<readonly string[]>([]);
  protected readonly showSummary = signal(false);
  protected readonly kindProblem = signal<string | null>(null);
  protected readonly confirming = signal<'archive' | 'restore' | null>(null);
  protected readonly working = signal(false);

  private readonly manage = this.session.permit(Permission.suppliersManage);
  protected readonly isEdit = computed(() => this.id() !== undefined);
  /** The person may change this firm: managers only. */
  protected readonly canChange = computed(() => this.manage());
  protected readonly archived = computed(() => this.loaded()?.isArchived ?? false);

  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  constructor() {
    effect(() => {
      const id = this.id();
      if (id) untracked(() => void this.load(id));
    });
    effect(() => {
      if (this.isEdit() && !this.canChange()) this.form.disable({ emitEvent: false });
    });
  }

  protected async load(id: string = this.id()!): Promise<void> {
    this.loadingFirm.set(true);
    this.error.set(null);
    try {
      const firm = await firstValueFrom(this.api.getSupplier(id));
      this.loaded.set(firm);
      this.form.reset({
        name: firm.name,
        isSupplier: firm.isSupplier,
        isSubcontractor: firm.isSubcontractor,
        tin: firm.tin ?? '',
        contactPerson: firm.contactPerson ?? '',
        phone: firm.phone ?? '',
        email: firm.email ?? '',
        address: firm.address ?? '',
        notes: firm.notes ?? '',
      });
      this.showSummary.set(false);
    } catch (failure) {
      this.error.set(asApiError(failure));
    } finally {
      this.loadingFirm.set(false);
    }
  }

  protected async submit(): Promise<void> {
    if (this.saving() || !this.canChange()) return;
    this.form.markAllAsTouched();
    const value = this.form.getRawValue();
    this.kindProblem.set(!value.isSupplier && !value.isSubcontractor ? 'Choose whether this firm is a supplier, a subcontractor, or both.' : null);
    if (this.form.invalid || this.kindProblem() !== null) {
      this.showSummary.set(true);
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }

    const text = (raw: string): string | null => (raw.trim() === '' ? null : raw.trim());
    const fields: SupplierFields = {
      name: value.name.trim(),
      isSupplier: value.isSupplier,
      isSubcontractor: value.isSubcontractor,
      tin: text(value.tin),
      contactPerson: text(value.contactPerson),
      phone: text(value.phone),
      email: text(value.email),
      address: text(value.address),
      notes: text(value.notes),
    };
    this.saving.set(true);
    this.showSummary.set(false);
    this.error.set(null);
    this.extraMessages.set([]);
    try {
      const current = this.loaded();
      const saved = await firstValueFrom(current ? this.api.updateSupplier(current.id, fields, current.version) : this.api.createSupplier(fields));
      this.toast.success(current ? 'Firm saved.' : `${saved.name} added.`);
      if (current) this.loaded.set(saved);
      else await this.router.navigate(['/purchasing/suppliers']);
    } catch (failure) {
      const problem = asApiError(failure);
      const unmatched = applyServerErrors(this.form, problem);
      const kind = problem.firstError('kind');
      if (kind) this.kindProblem.set(kind);
      const outside = unmatched.filter((message) => message !== kind);
      if (problem.isConflict || problem.isRuleViolation || problem.isServerFailure || !problem.isValidation || outside.length > 0) {
        this.error.set(problem);
        this.extraMessages.set(outside);
      }
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }

  protected async confirmed(): Promise<void> {
    const firm = this.loaded();
    const step = this.confirming();
    if (!firm || !step || this.working()) return;
    this.working.set(true);
    try {
      const changed = await firstValueFrom(step === 'archive' ? this.api.archiveSupplier(firm.id, firm.version) : this.api.restoreSupplier(firm.id, firm.version));
      this.loaded.set(changed);
      this.toast.success(step === 'archive' ? `${changed.name} archived.` : `${changed.name} restored.`);
      this.confirming.set(null);
    } catch (failure) {
      this.confirming.set(null);
      this.error.set(asApiError(failure));
    } finally {
      this.working.set(false);
    }
  }
}
