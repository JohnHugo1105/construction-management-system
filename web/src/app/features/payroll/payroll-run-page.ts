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
import { PAYROLL_STATUS, periodLabel, runActions } from './payroll-rules';
import { PayrollRunState } from './payroll-run-state';
import { LineView, PayrollLineDetail } from './payroll-line-detail';
import { PayrollLine, PayrollRun } from './payroll.models';
import { VoidRunDialog } from './void-run-dialog';

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
  protected readonly expanded = signal<ReadonlySet<string>>(new Set());

  protected readonly title = computed(() =>
    this.state.status() === 'ready'
      ? `Payroll ${periodLabel(this.state.run().periodStart, this.state.run().periodEnd)}`
      : 'Payroll run',
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
      hours: hoursWords(line.regularHours, line.overtimeHours),
      gross: money(line.grossPayMinor),
      deductions: money(line.deductionsMinor),
      net: money(line.netPayMinor),
      regularPay: money(line.regularPayMinor),
      overtimePay: money(line.overtimePayMinor),
      deductionRows: line.deductions.map((item) => ({
        id: item.id,
        description: item.description,
        amount: money(item.amountMinor),
      })),
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

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => void this.state.load(id));
    });
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
    void this.state.reload();
  }
}
