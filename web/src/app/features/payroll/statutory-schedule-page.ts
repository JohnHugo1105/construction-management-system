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
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { injectFocusFirstInvalid, setServerError } from '../../shared/forms/server-errors';
import { formatDate } from '../../shared/presentation/business-time';
import { formatMoney } from '../../shared/presentation/money';
import { formatBps } from '../../shared/presentation/percent';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FieldGroup } from '../../shared/ui/form-field/field-group';
import { FormField } from '../../shared/ui/form-field/form-field';
import { Icon } from '../../shared/ui/icon/icon';
import { MoneyInput } from '../../shared/ui/inputs/money-input';
import { PercentInput } from '../../shared/ui/inputs/percent-input';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { describeFormFailure } from '../workforce/form-failure';
import { newRequestId } from '../workforce/request-id';
import { StatutoryAgency } from './payroll.models';
import { StatutoryApi } from './statutory-api';
import {
  AGENCIES,
  AGENCY_LABEL,
  BASIS_LABEL,
  METHOD_HINT,
  METHOD_LABEL,
  methodsFor,
  rowRange,
  rowShares,
} from './statutory-rules';
import {
  SaveScheduleRequest,
  StatutoryMethod,
  StatutoryPeriodBasis,
  StatutoryRow,
  StatutorySchedule,
  StatutoryStatus,
} from './statutory.models';

type RowForm = FormGroup<{
  to: FormControl<number | null>;
  employee: FormControl<number | null>;
  employer: FormControl<number | null>;
  baseTax: FormControl<number | null>;
  rate: FormControl<number | null>;
}>;

/** The API's field names for the percentage figures; the form leaves off "Bps" and "Minor". */
const SERVER_FIELD_NAMES = {
  employeeRateBps: 'employeeRate',
  employerRateBps: 'employerRate',
  baseFloorMinor: 'baseFloor',
  baseCeilingMinor: 'baseCeiling',
  employeeMinimumMinor: 'employeeMinimum',
  employeeMaximumMinor: 'employeeMaximum',
  employerMinimumMinor: 'employerMinimum',
  employerMaximumMinor: 'employerMaximum',
  // Row problems name their row, so they are listed above the form rather than put on the list of rows.
  rows: 'rowProblems',
} as const;

/**
 * One contribution or tax table (`/payroll/statutory/new` and `/payroll/statutory/:id`). An accountant enters it from the
 * official schedule: the agency, when it takes effect, how it works and its figures. A bracket table is entered one row per
 * card, each starting where the one before ends, so it can't have gaps. A table an approved payroll used, a retired one, or any
 * table for someone who only reads, is shown instead of edited, with "Copy to a new table" for people who manage tables.
 */
@Component({
  selector: 'app-statutory-schedule-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    PageHeader,
    FormField,
    FieldGroup,
    Control,
    MoneyInput,
    PercentInput,
    Button,
    Icon,
    Alert,
    ApiErrorAlert,
    Skeleton,
    StatusChip,
    Dialog,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './statutory-schedule-page.html',
  styleUrl: './statutory-schedule-page.scss',
})
export class StatutorySchedulePage {
  /** The table's id from the route; absent on `/new`. */
  readonly id = input<string | undefined>(undefined);
  /** `?copy=<id>` on `/new` starts from another table's figures. */
  readonly copy = input<string | undefined>(undefined);

  private readonly api = inject(StatutoryApi);
  private readonly session = inject(SessionState);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly agencies = AGENCIES;
  protected readonly agencyLabel = AGENCY_LABEL;
  protected readonly methodLabel = METHOD_LABEL;
  protected readonly methodHint = METHOD_HINT;
  protected readonly basisLabel = BASIS_LABEL;
  protected readonly bases: readonly StatutoryPeriodBasis[] = ['Monthly', 'PerRun'];

  protected readonly canManage = this.session.permit(Permission.statutoryManage);
  protected readonly loaded = signal<StatutorySchedule | null>(null);
  protected readonly status = signal<StatutoryStatus | null>(null);
  protected readonly loading = signal(false);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);
  protected readonly showSummary = signal(false);
  protected readonly retiring = signal(false);
  protected readonly retireError = signal<ApiError | null>(null);
  protected readonly copiedFrom = signal<string | null>(null);

  protected readonly form = new FormGroup({
    agency: new FormControl<StatutoryAgency | null>(null, { validators: [Validators.required] }),
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(120)],
    }),
    effectiveFrom: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    method: new FormControl<StatutoryMethod | null>(null, { validators: [Validators.required] }),
    periodBasis: new FormControl<StatutoryPeriodBasis | null>('Monthly', {
      validators: [Validators.required],
    }),
    employeeRate: new FormControl<number | null>(null),
    employerRate: new FormControl<number | null>(null),
    baseFloor: new FormControl<number | null>(null),
    baseCeiling: new FormControl<number | null>(null),
    employeeMinimum: new FormControl<number | null>(null),
    employeeMaximum: new FormControl<number | null>(null),
    employerMinimum: new FormControl<number | null>(null),
    employerMaximum: new FormControl<number | null>(null),
    rows: new FormArray<RowForm>([]),
  });

  /** The chosen agency and method as signals, so the template shows only the fields that apply. */
  protected readonly agency = signal<StatutoryAgency | null>(null);
  protected readonly method = signal<StatutoryMethod | null>(null);
  protected readonly methods = computed(() => {
    const agency = this.agency();
    return agency ? methodsFor(agency) : [];
  });

  protected readonly isNew = computed(() => !this.id());
  /** Edited in place only by a manager, while active and not used by an approved payroll. */
  protected readonly editable = computed(() => {
    if (!this.canManage()) return false;
    const schedule = this.loaded();
    if (!schedule) return this.isNew();
    return schedule.status === 'Active' && !schedule.usedByApprovedPayroll;
  });

  protected readonly title = computed(() => {
    if (this.isNew()) return 'Add a table';
    const schedule = this.loaded();
    return schedule ? `${schedule.agencyLabel} table` : 'Table';
  });

  /** The table in force for the chosen agency, offered as a starting point on a new table. */
  protected readonly copyCandidate = computed(() => {
    const agency = this.agency();
    if (!this.isNew() || !agency || this.copiedFrom()) return null;
    return (
      this.status()?.agencies.find(
        (item) => item.agency === agency && item.state === 'Available',
      ) ?? null
    );
  });

  /** The read-only view of a table. */
  protected readonly view = computed(() => {
    const schedule = this.loaded();
    if (!schedule) return null;
    const currency = this.session.currencyCode();
    const money = (minor: number | null): string | null =>
      minor === null ? null : formatMoney(minor, currency);
    const percentage =
      schedule.method === 'Percentage'
        ? [
            { label: 'Employee', value: formatBps(schedule.employeeRateBps ?? 0) },
            { label: 'Employer', value: formatBps(schedule.employerRateBps ?? 0) },
            { label: 'Lowest pay it applies to', value: money(schedule.baseFloorMinor) ?? 'None' },
            {
              label: 'Highest pay it applies to',
              value: money(schedule.baseCeilingMinor) ?? 'No limit',
            },
            { label: "Employee's smallest share", value: money(schedule.employeeMinimumMinor) },
            { label: "Employee's largest share", value: money(schedule.employeeMaximumMinor) },
            { label: "Employer's smallest share", value: money(schedule.employerMinimumMinor) },
            { label: "Employer's largest share", value: money(schedule.employerMaximumMinor) },
          ].filter((item): item is { label: string; value: string } => item.value !== null)
        : [];
    return {
      name: schedule.name,
      facts: `In force from ${formatDate(schedule.effectiveFrom)} · ${METHOD_LABEL[schedule.method]} · ${BASIS_LABEL[schedule.periodBasis].toLowerCase()}`,
      chip:
        schedule.status === 'Retired'
          ? { label: 'Retired', tone: 'neutral' as const, icon: 'archive' as const }
          : schedule.inForce
            ? { label: 'In force', tone: 'success' as const, icon: 'check-circle' as const }
            : null,
      used: schedule.usedByApprovedPayroll,
      retired: schedule.status === 'Retired',
      percentage,
      rows: schedule.rows.map((row, index) => ({
        key: index,
        range: rowRange(row, currency),
        shares: rowShares(row, schedule.method, currency),
      })),
    };
  });

  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();
  private readonly requestId = newRequestId();

  constructor() {
    this.form.controls.agency.valueChanges.pipe(takeUntilDestroyed()).subscribe((agency) => {
      this.agency.set(agency);
      const allowed = agency ? methodsFor(agency) : [];
      const current = this.form.controls.method.value;
      if (allowed.length === 1) this.form.controls.method.setValue(allowed[0] ?? null);
      else if (current && !allowed.includes(current)) this.form.controls.method.setValue(null);
    });
    this.form.controls.method.valueChanges.pipe(takeUntilDestroyed()).subscribe((method) => {
      this.method.set(method);
      if (method !== 'Percentage' && this.rows.length === 0) this.addRow();
    });

    effect(() => {
      const id = this.id();
      const copy = this.copy();
      untracked(() => void this.load(id, copy));
    });
  }

  protected get rows(): FormArray<RowForm> {
    return this.form.controls.rows;
  }

  /** Where a row starts: 0 for the first, otherwise where the row before it ends. */
  protected fromOf(index: number): string {
    const from = index === 0 ? 0 : this.rows.at(index - 1)?.controls.to.value;
    return from === null || from === undefined
      ? `the end of row ${index}`
      : formatMoney(from, this.session.currencyCode());
  }

  protected addRow(): void {
    this.rows.push(this.newRow(null));
  }

  protected removeRow(index: number): void {
    this.rows.removeAt(index);
    // The new last row covers every larger amount.
    this.rows.at(this.rows.length - 1)?.controls.to.setValue(null);
  }

  protected async copyFrom(id: string): Promise<void> {
    try {
      this.fill(await firstValueFrom(this.api.get(id)), true);
      this.copiedFrom.set(id);
    } catch (failure) {
      this.error.set(asApiError(failure));
    }
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    this.checkConditionalFields();
    if (this.form.invalid) {
      this.showSummary.set(true);
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }

    const value = this.form.getRawValue();
    const method = value.method as StatutoryMethod;
    const percentage = method === 'Percentage';
    const rows: StatutoryRow[] = percentage
      ? []
      : value.rows.map((row, index, all) => ({
          fromMinor: index === 0 ? 0 : (all[index - 1]?.to ?? 0),
          toMinor: index === all.length - 1 ? null : row.to,
          employeeMinor: row.employee ?? 0,
          employerMinor: row.employer ?? 0,
          baseTaxMinor: row.baseTax ?? 0,
          rateBps: row.rate ?? 0,
        }));
    const existing = this.loaded();
    const request: SaveScheduleRequest = {
      agency: (existing?.agency ?? value.agency) as StatutoryAgency,
      name: value.name.trim(),
      effectiveFrom: value.effectiveFrom,
      method,
      periodBasis: value.periodBasis as StatutoryPeriodBasis,
      employeeRateBps: percentage ? value.employeeRate : null,
      employerRateBps: percentage ? value.employerRate : null,
      baseFloorMinor: percentage ? (value.baseFloor ?? 0) : null,
      baseCeilingMinor: percentage ? value.baseCeiling : null,
      employeeMinimumMinor: percentage ? value.employeeMinimum : null,
      employeeMaximumMinor: percentage ? value.employeeMaximum : null,
      employerMinimumMinor: percentage ? value.employerMinimum : null,
      employerMaximumMinor: percentage ? value.employerMaximum : null,
      rows: percentage ? null : rows,
      clientRequestId: existing ? null : this.requestId,
      version: existing?.version ?? 0,
    };

    this.saving.set(true);
    this.showSummary.set(false);
    this.error.set(null);
    this.extra.set([]);
    try {
      const saved = await firstValueFrom(
        existing ? this.api.update(existing.id, request) : this.api.create(request),
      );
      this.toast.success(existing ? 'Table saved.' : 'Table added.');
      if (existing) this.fill(saved, false);
      else await this.router.navigate(['/payroll/statutory', saved.id]);
    } catch (failure) {
      const outcome = describeFormFailure(this.form, asApiError(failure), SERVER_FIELD_NAMES);
      this.error.set(outcome.alert ?? (outcome.extra.length > 0 ? asApiError(failure) : null));
      this.extra.set(outcome.extra);
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }

  protected async retire(): Promise<void> {
    const schedule = this.loaded();
    if (!schedule || this.saving()) return;
    this.saving.set(true);
    this.retireError.set(null);
    try {
      await firstValueFrom(this.api.retire(schedule.id, schedule.version));
      this.retiring.set(false);
      this.toast.success('Table retired.');
      await this.router.navigate(['/payroll/statutory']);
    } catch (failure) {
      this.retireError.set(asApiError(failure));
    } finally {
      this.saving.set(false);
    }
  }

  protected reload(): void {
    this.retiring.set(false);
    void this.load(this.id(), this.copy());
  }

  private async load(id: string | undefined, copy: string | undefined): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);
    try {
      if (id) {
        this.fill(await firstValueFrom(this.api.get(id)), false);
      } else {
        if (this.canManage()) this.status.set(await firstValueFrom(this.api.status()));
        if (copy) await this.copyFrom(copy);
      }
    } catch (failure) {
      this.loadError.set(asApiError(failure));
    } finally {
      this.loading.set(false);
    }
  }

  /** Puts a table into the form. A copy keeps the figures and asks for a new effective date. */
  private fill(schedule: StatutorySchedule, asCopy: boolean): void {
    if (!asCopy) this.loaded.set(schedule);
    this.rows.clear();
    this.form.reset({
      agency: schedule.agency,
      name: schedule.name,
      effectiveFrom: asCopy ? '' : schedule.effectiveFrom,
      method: schedule.method,
      periodBasis: schedule.periodBasis,
      employeeRate: schedule.employeeRateBps,
      employerRate: schedule.employerRateBps,
      baseFloor: schedule.baseFloorMinor,
      baseCeiling: schedule.baseCeilingMinor,
      employeeMinimum: schedule.employeeMinimumMinor,
      employeeMaximum: schedule.employeeMaximumMinor,
      employerMinimum: schedule.employerMinimumMinor,
      employerMaximum: schedule.employerMaximumMinor,
    });
    this.rows.clear();
    for (const row of schedule.rows) this.rows.push(this.newRow(row));
    this.agency.set(schedule.agency);
    this.method.set(schedule.method);
  }

  /** Fields that are needed only for some methods, and the rows' upper limits (every row but the last). */
  private checkConditionalFields(): void {
    const method = this.form.controls.method.value;
    if (method === 'Percentage') {
      if (this.form.controls.employeeRate.value === null)
        setServerError(this.form.controls.employeeRate, "Enter the employee's rate.");
      if (this.form.controls.employerRate.value === null)
        setServerError(this.form.controls.employerRate, "Enter the employer's rate.");
      return;
    }
    if (!method) return;
    this.rows.controls.forEach((row, index) => {
      const last = index === this.rows.length - 1;
      if (!last && row.controls.to.value === null)
        setServerError(row.controls.to, 'Enter where this row ends.');
      if (method === 'Brackets') {
        if (row.controls.employee.value === null)
          setServerError(row.controls.employee, "Enter the employee's share.");
        if (row.controls.employer.value === null)
          setServerError(row.controls.employer, "Enter the employer's share.");
      } else if (row.controls.rate.value === null) {
        setServerError(row.controls.rate, 'Enter the rate (0% when there is none).');
      }
    });
  }

  private newRow(row: StatutoryRow | null): RowForm {
    return new FormGroup({
      to: new FormControl<number | null>(row?.toMinor ?? null),
      employee: new FormControl<number | null>(row ? row.employeeMinor : null),
      employer: new FormControl<number | null>(row ? row.employerMinor : null),
      baseTax: new FormControl<number | null>(row ? row.baseTaxMinor : null),
      rate: new FormControl<number | null>(row ? row.rateBps : null),
    });
  }
}
