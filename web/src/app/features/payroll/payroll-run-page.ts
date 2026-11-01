import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { ToastService } from '../../core/notifications/toast-service';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { formatDateTime } from '../../shared/presentation/business-time';
import { formatMoney } from '../../shared/presentation/money';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Icon } from '../../shared/ui/icon/icon';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { hoursWords } from '../workforce/attendance-sheet';
import { ApproveRunDialog } from './approve-run-dialog';
import { DeductionsDialog } from './deductions-dialog';
import { payrollExportUrl } from './payroll-api';
import { PAYROLL_STATUS, runActions } from './payroll-rules';
import { archivedPostingsNote, holidayNote, payBreakdown, runTitle } from './premium-rules';
import { PayrollRunState } from './payroll-run-state';
import { LineView, PayrollLineDetail } from './payroll-line-detail';
import { PayrollDeduction, PayrollLine, PayrollRun } from './payroll.models';
import { RecalculateStatutoryDialog } from './recalculate-statutory-dialog';
import { StatutoryApi } from './statutory-api';
import { deductionSource, needsSetupLines } from './statutory-rules';
import { PayrollStatutory, StatutoryStatus } from './statutory.models';
import { VoidRunDialog } from './void-run-dialog';
import { AdjustStatutoryDialog } from './adjust-statutory-dialog';
import { firstValueFrom } from 'rxjs';

/**
 * One payroll run: its period and state, the API's totals, and a line for each worker that opens to show how the pay was made
 * up, what was deducted and which projects it is charged to. What a person can do follows what they hold and what state the run
 * is in: prepare people enter deductions and void a draft, approvers approve and void an approved payroll, and a person who
 * may only read sees the figures and no buttons. Every figure shown comes from the API.
 */
@Component({
  selector: 'app-payroll-run-page',
  imports: [
    RouterLink,
    PageHeader,
    Alert,
    ApiErrorAlert,
    Button,
    Icon,
    Skeleton,
    StatusChip,
    ApproveRunDialog,
    DeductionsDialog,
    PayrollLineDetail,
    VoidRunDialog,
    AdjustStatutoryDialog,
    RecalculateStatutoryDialog,
  ],
  providers: [PayrollRunState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './payroll-run-page.html',
  styleUrl: './payroll-run-page.scss',
})
export class PayrollRunPage {
  /** Bound from the route (`/payroll/:id`). */
  readonly id = input.required<string>();

  protected readonly state = inject(PayrollRunState);
  protected readonly viewport = inject(Viewport);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);

  protected readonly approving = signal(false);
  protected readonly voiding = signal(false);
  protected readonly editing = signal<PayrollLine | null>(null);
  protected readonly adjusting = signal<{ line: PayrollLine; deduction: PayrollDeduction } | null>(
    null,
  );
  protected readonly recalculating = signal(false);
  private readonly statutoryApi = inject(StatutoryApi);
  protected readonly statutory = signal<PayrollStatutory | null>(null);
  protected readonly statutoryStatus = signal<StatutoryStatus | null>(null);
  protected readonly expanded = signal<ReadonlySet<string>>(new Set());

  protected readonly title = computed(() =>
    this.state.status() === 'ready' ? runTitle(this.state.run()) : 'Payroll run',
  );

  protected readonly thirteenthMonth = computed(
    () => this.state.status() === 'ready' && this.state.run().kind === 'ThirteenthMonth',
  );

  /** One quiet line when the company has no holiday entered for the payroll's year (the API decides). */
  protected readonly holidayNote = computed(() =>
    this.state.status() === 'ready' ? holidayNote(this.state.run()) : null,
  );

  /** One quiet line when the server left workers posted to archived projects out of holiday pay. */
  protected readonly archivedNote = computed(() =>
    this.state.status() === 'ready' ? archivedPostingsNote(this.state.run()) : null,
  );

  protected readonly actions = computed(() =>
    runActions(this.state.run().status, {
      canRun: this.session.can(Permission.payrollRun),
      canApprove: this.session.can(Permission.payrollApprove),
    }),
  );
  protected readonly canRun = this.session.permit(Permission.payrollRun);

  protected readonly summary = computed(() => {
    if (this.state.status() !== 'ready') return null;
    const run = this.state.run();
    const zone = this.session.timeZoneId();
    const currency = this.session.currencyCode();
    const history: string[] = [];
    if (run.createdByName)
      history.push(`Prepared by ${run.createdByName} on ${formatDateTime(run.createdAtUtc, zone)}`);
    if (run.approvedAtUtc)
      history.push(
        `Approved${run.approvedByName ? ` by ${run.approvedByName}` : ''} on ${formatDateTime(run.approvedAtUtc, zone)}`,
      );
    if (run.voidedAtUtc)
      history.push(
        `Voided${run.voidedByName ? ` by ${run.voidedByName}` : ''} on ${formatDateTime(run.voidedAtUtc, zone)}`,
      );
    return {
      status: PAYROLL_STATUS[run.status],
      workers: run.workerCount === 1 ? '1 worker' : `${run.workerCount} workers`,
      history,
      voidReason: run.voidReason,
      gross: formatMoney(run.totalGrossMinor, currency),
      deductions: formatMoney(run.totalDeductionsMinor, currency),
      net: formatMoney(run.totalNetMinor, currency),
      draft: run.status === 'Draft',
      exportUrl: payrollExportUrl(run.id),
    };
  });

  /** The lines with their figures and words worked out, so the template only prints them. */
  protected readonly lines = computed<readonly LineView[]>(() => {
    if (this.state.status() !== 'ready') return [];
    const currency = this.session.currencyCode();
    const money = (minor: number): string => formatMoney(minor, currency);
    return this.state.run().lines.map((line) => ({
      line,
      id: line.workerId,
      name: line.workerName,
      sub: `${line.employeeNumber} · ${line.designation}`,
      hours:
        (line.thirteenthMonthPayMinor ?? 0) > 0
          ? `Basic pay ${money(line.thirteenthMonthBasisMinor ?? 0)}`
          : hoursWords(line.regularHours, line.overtimeHours),
      gross: money(line.grossPayMinor),
      deductions: money(line.deductionsMinor),
      net: money(line.netPayMinor),
      breakdown: payBreakdown(line, money),
      deductionRows: line.deductions.map((item) => ({
        id: item.id,
        description: item.description,
        amount: money(item.amountMinor),
        source: deductionSource(item),
        reason: item.isAdjusted ? item.adjustmentReason : null,
        deduction: item,
      })),
      employerShares: line.employerSharesMinor > 0 ? money(line.employerSharesMinor) : null,
      limited: line.statutoryLimited,
      shares: line.allocations.map((share, index) => ({
        key: `${share.projectId}|${share.subProjectId ?? ''}|${index}`,
        place: share.subProjectName
          ? `${share.projectName}, ${share.subProjectName}`
          : share.projectName,
        hours: hoursWords(share.regularHours, share.overtimeHours),
        amount: money(share.grossMinor),
      })),
    }));
  });

  /** The calculated contributions owed for this run, per agency, from the API (null until loaded, or when there are none). */
  protected readonly remit = computed(() => {
    const data = this.statutory();
    if (!data || data.agencies.length === 0) return null;
    const currency = this.session.currencyCode();
    const money = (minor: number): string => formatMoney(minor, currency);
    return {
      total: money(data.totalToRemitMinor),
      agencies: data.agencies.map((agency) => ({
        key: agency.agency,
        label: agency.label,
        workers: agency.workerCount === 1 ? '1 worker' : `${agency.workerCount} workers`,
        employee: money(agency.employeeMinor),
        employer: agency.agency === 'WithholdingTax' ? null : money(agency.employerMinor),
        toRemit: money(agency.toRemitMinor),
      })),
    };
  });

  /** One quiet line per agency that is not worked out yet; only on a draft, where it can still be entered. */
  protected readonly setupLines = computed(() =>
    this.state.status() === 'ready' &&
    this.state.run().status === 'Draft' &&
    !this.thirteenthMonth()
      ? needsSetupLines(this.statutoryStatus())
      : [],
  );

  /** A draft is worked out again from the current pay rules, holidays and tables by whoever prepares payroll. */
  protected readonly canRecalculate = computed(() => this.actions().editDeductions);

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => void this.state.load(id));
    });
    // Each time the run changes (loaded, or an action answered), read what it owes the agencies; the status only once.
    effect(() => {
      if (this.state.status() !== 'ready') return;
      const run = this.state.run();
      // 13th-month pay carries no contributions or tax, so there is nothing to ask about.
      if (run.kind === 'ThirteenthMonth') return;
      untracked(() => void this.loadStatutory(run));
    });
  }

  private async loadStatutory(run: PayrollRun): Promise<void> {
    try {
      if (run.status === 'Draft' && this.statutoryStatus() === null)
        this.statutoryStatus.set(await firstValueFrom(this.statutoryApi.status(run.periodEnd)));
      this.statutory.set(await firstValueFrom(this.statutoryApi.runStatutory(run.id)));
    } catch {
      // The contributions summary is extra: the run and its deductions are already on screen, so a failure here stays quiet.
      this.statutory.set(null);
    }
  }

  protected adjustSaved(run: PayrollRun): void {
    this.adjusting.set(null);
    this.state.replace(run);
    this.toast.success('Amount saved.');
  }

  protected recalculated(run: PayrollRun): void {
    this.recalculating.set(false);
    this.state.replace(run);
    this.toast.success('Payroll recalculated.');
  }

  protected isOpen(workerId: string): boolean {
    return this.expanded().has(workerId);
  }

  protected toggle(workerId: string): void {
    const next = new Set(this.expanded());
    if (next.has(workerId)) next.delete(workerId);
    else next.add(workerId);
    this.expanded.set(next);
  }

  protected approved(run: PayrollRun): void {
    this.approving.set(false);
    this.state.replace(run);
    this.toast.success('Payroll approved.');
  }

  protected voided(run: PayrollRun): void {
    this.voiding.set(false);
    this.state.replace(run);
    this.toast.success('Payroll voided.');
  }

  protected deductionsSaved(run: PayrollRun): void {
    this.editing.set(null);
    this.state.replace(run);
    this.toast.success('Deductions saved.');
  }

  /** After a refusal the dialogs offer to reload: close whatever is open and fetch the run again. */
  protected reload(): void {
    this.approving.set(false);
    this.voiding.set(false);
    this.editing.set(null);
    this.adjusting.set(null);
    this.recalculating.set(false);
    void this.state.reload();
  }
}
