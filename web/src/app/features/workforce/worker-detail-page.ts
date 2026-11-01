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
import { formatDate } from '../../shared/presentation/business-time';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { ChangeStatusDialog } from './change-status-dialog';
import { ReinstateDialog } from './reinstate-dialog';
import { WorkerAttendancePanel } from './worker-attendance-panel';
import { WorkerCasesPanel } from './worker-cases-panel';
import { WorkerDetailState } from './worker-detail-state';
import { WorkerPayPanel } from './worker-pay-panel';
import { WorkerPostingPanel } from './worker-posting-panel';
import { WORKER_STATUS, hasLeft } from './worker-status';
import { WorkerDetail } from './workforce.models';

/**
 * One worker. The top says who they are and where they stand; the sections below answer one question each: where they are
 * posted, what they are paid (only for people who may see pay), how they have been turning up, what has been noted, and their
 * personal details. A worker who has left offers one next step: bring them back.
 */
@Component({
  selector: 'app-worker-detail-page',
  imports: [
    RouterLink,
    PageHeader,
    Alert,
    ApiErrorAlert,
    Button,
    Skeleton,
    StatusChip,
    ChangeStatusDialog,
    ReinstateDialog,
    WorkerPostingPanel,
    WorkerPayPanel,
    WorkerAttendancePanel,
    WorkerCasesPanel,
  ],
  providers: [WorkerDetailState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './worker-detail-page.html',
  styleUrl: './worker-detail-page.scss',
})
export class WorkerDetailPage {
  /** Bound from the route (`/workers/:id`). */
  readonly id = input.required<string>();

  protected readonly state = inject(WorkerDetailState);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);

  protected readonly canManage = this.session.permit(Permission.workforceManage);
  protected readonly changingStatus = signal(false);
  protected readonly reinstating = signal(false);

  protected readonly summary = computed(() => {
    if (this.state.status() !== 'ready') return null;
    const worker = this.state.worker();
    const left = hasLeft(worker.status);
    return {
      status: WORKER_STATUS[worker.status],
      about: [worker.designation, worker.employeeNumber].filter(Boolean).join(' · '),
      left,
      since: this.statusSince(worker),
      reason: worker.statusReason,
      details: this.details(worker),
    };
  });

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => void this.state.load(id));
    });
  }

  protected statusChanged(updated: WorkerDetail): void {
    this.changingStatus.set(false);
    this.state.replace(updated);
    this.toast.success(
      `${updated.fullName} is now ${WORKER_STATUS[updated.status].label.toLowerCase()}.`,
    );
  }

  protected reinstated(updated: WorkerDetail): void {
    this.reinstating.set(false);
    this.state.replace(updated);
    this.toast.success(`${updated.fullName} is active again.`);
  }

  protected reload(): void {
    this.changingStatus.set(false);
    this.reinstating.set(false);
    void this.state.reload();
  }

  /** "Since 3 Nov 2026" for a state that began on a date; nothing for an active worker. */
  private statusSince(worker: WorkerDetail): string {
    if (worker.status === 'Active' || !worker.statusChangedOn) return '';
    return `Since ${formatDate(worker.statusChangedOn)}`;
  }

  /** The personal details that have been filled in, as label and value pairs. Empty ones are left out. */
  private details(
    worker: WorkerDetail,
  ): readonly { readonly label: string; readonly value: string }[] {
    const rows: { label: string; value: string | null }[] = [
      { label: 'Hired', value: formatDate(worker.dateHired) },
      { label: 'Contact number', value: worker.contactNumber },
      { label: 'Date of birth', value: worker.dateOfBirth ? formatDate(worker.dateOfBirth) : null },
      { label: 'Marital status', value: worker.maritalStatus },
      { label: 'Spouse', value: worker.spouseName },
      { label: 'Religion', value: worker.religion },
      { label: 'Education', value: worker.educationalAttainment },
      { label: 'Present address', value: worker.presentAddress },
      { label: 'Provincial address', value: worker.provincialAddress },
      { label: 'Reference', value: worker.reference },
    ];
    return rows.filter((row): row is { label: string; value: string } => !!row.value);
  }
}
