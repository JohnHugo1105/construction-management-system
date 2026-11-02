import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  numberAttribute,
  signal,
  untracked,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { formatDate, todayIn } from '../../shared/presentation/business-time';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Pager } from '../../shared/ui/pager/pager';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { TimeClockApi } from './timeclock-api';
import { AddScanDialog, ImportScansDialog, RemoveScanDialog } from './timeclock-dialogs';
import {
  EXCEPTION_CHIPS,
  clockTime,
  directionWords,
  hoursLine,
  hoursText,
  scanLine,
  workers,
} from './timeclock-presentation';
import {
  ConfirmResult,
  PunchDirection,
  TimeClockDayList,
  TimeClockDayRow,
  TimeClockPunch,
} from './timeclock.models';

/**
 * Reviewing the time clock (`/time-clock`, timeclock.review): pick a project and a day (by default the earliest day still waiting), see each worker's
 * day worked out from the scans, approve overtime row by row, fix a missing or wrong scan, and confirm. Confirming records the attendance; nothing is
 * confirmed by itself. "Confirm all clean rows" is the one primary action. Hours only: pay is never shown here.
 */
@Component({
  selector: 'app-time-clock-review-page',
  imports: [
    RouterLink,
    PageHeader,
    Alert,
    ApiErrorAlert,
    Button,
    EmptyState,
    Pager,
    Skeleton,
    StatusChip,
    AddScanDialog,
    RemoveScanDialog,
    ImportScansDialog,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header
      title="Time clock"
      description="Scans from the time clock, one day at a time. Confirm a day to record it as attendance."
    >
      @if (canManage()) {
        <a appButton routerLink="/time-clock/badges">Badges</a>
      }
      @if (canManage() && day()) {
        <button appButton type="button" (click)="importing.set(true)">
          Import a machine's file
        </button>
      }
    </app-page-header>

    <div class="stack">
      <div class="review-bar">
        <label class="field">
          <span class="field__label">Project</span>
          <select class="input" (change)="chooseProject($event)">
            @if (!projectId()) {
              <option value="" selected>Choose a project</option>
            }
            @for (project of projects(); track project.id) {
              <option [value]="project.id" [selected]="project.id === projectId()">
                {{ project.name }}
              </option>
            }
          </select>
        </label>
        @if (projectId()) {
          <label class="field">
            <span class="field__label">Day</span>
            <input
              class="input"
              type="date"
              [max]="today()"
              [value]="day()?.workDate ?? date() ?? ''"
              (change)="chooseDate($event)"
            />
          </label>
        }
      </div>

      @if (!projectId()) {
        <app-empty-state
          icon="clock"
          title="Choose a project"
          message="See the scans of its crew a day at a time, and confirm them as attendance."
        />
      } @else if (loadError(); as problem) {
        <app-api-error-alert
          [error]="problem"
          [allowRetry]="true"
          reloadLabel="Try again"
          (reload)="reload()"
        />
      } @else if (loading() && !day()) {
        <app-skeleton variant="card" [lines]="4" label="Loading the day" />
      } @else if (day(); as list) {
        @if (list.projectArchived) {
          <app-alert tone="warning" title="This project is archived"
            >Its scans can be read but not confirmed. Restore the project first.</app-alert
          >
        }
        @if (waitingElsewhere(); as earliest) {
          <app-alert tone="info" [title]="waitingTitle()">
            <div alertActions>
              <button appButton type="button" (click)="goTo(earliest)">
                Go to {{ formatDate(earliest) }}
              </button>
            </div>
          </app-alert>
        }
        @if (actionError(); as problem) {
          <app-api-error-alert [error]="problem" reloadLabel="Reload the day" (reload)="reload()" />
        }
        @if (skipped().length > 0) {
          <app-alert tone="warning" title="Some workers weren't confirmed">
            @for (item of skipped(); track item.workerId) {
              <span class="review-skip"
                >{{ item.workerName || 'A worker' }}: {{ item.reason }}</span
              >
            }
          </app-alert>
        }

        @if (list.items.length === 0) {
          <app-empty-state
            icon="clock"
            [title]="'No scans on ' + formatDate(list.workDate)"
            message="Scans appear here as the crew scans in and out at the time clock."
          />
        } @else {
          <ul class="review-rows">
            @for (row of list.items; track row.workerId) {
              <li class="card review-row" [class.review-row--done]="row.alreadyRecorded">
                <div class="review-row__head">
                  <h2 class="review-row__name">{{ row.workerName }}</h2>
                  @if (row.alreadyRecorded) {
                    <app-status-chip label="Already recorded" tone="neutral" icon="check" />
                  }
                  @for (flag of row.exceptions; track flag) {
                    <app-status-chip
                      [label]="chips[flag].label"
                      [tone]="chips[flag].tone"
                      [icon]="chips[flag].icon"
                    />
                  }
                </div>
                @if (scanLine(row)) {
                  <p>{{ scanLine(row) }}</p>
                }
                <p class="review-row__hours">{{ hoursLine(row) }}</p>
                @if (row.exceptions.length > 0 && !row.alreadyRecorded) {
                  <p class="review-muted">{{ chips[row.exceptions[0]!].help }}</p>
                }
                @if (row.proposedOvertimeHours > 0 && !row.alreadyRecorded) {
                  <label class="choice review-overtime">
                    <input
                      type="checkbox"
                      [checked]="approved()[row.workerId] === true"
                      (change)="approve(row.workerId, $event)"
                    />
                    <span class="choice__text">
                      <span class="choice__label"
                        >Approve {{ hoursText(row.proposedOvertimeHours) }} overtime</span
                      >
                      <span class="choice__hint"
                        >Overtime is recorded only when you approve it.</span
                      >
                    </span>
                  </label>
                }
                @if (!row.alreadyRecorded && !list.projectArchived) {
                  <div class="review-row__actions">
                    @if (row.canConfirm) {
                      <button
                        appButton
                        type="button"
                        [busy]="confirming() === row.workerId"
                        (click)="confirmRow(row)"
                      >
                        Confirm {{ row.workerName }}
                      </button>
                    }
                    <button appButton variant="ghost" type="button" (click)="adding.set(row)">
                      Add a missing scan
                    </button>
                  </div>
                }
                <details class="review-scans">
                  <summary>
                    {{ row.punches.length === 1 ? '1 scan' : row.punches.length + ' scans' }}
                  </summary>
                  <ul>
                    @for (punch of row.punches; track punch.id) {
                      <li>
                        <span>
                          {{ directionWords(punch.direction) }} {{ clockTime(punch.localTime) }}
                          <span class="review-muted">{{ source(punch) }}</span>
                        </span>
                        @if (!row.alreadyRecorded && !list.projectArchived) {
                          <button
                            appButton
                            variant="ghost"
                            type="button"
                            [attr.aria-label]="
                              'Remove the ' +
                              directionWords(punch.direction).toLowerCase() +
                              ' scan at ' +
                              clockTime(punch.localTime)
                            "
                            (click)="removing.set({ punch, name: row.workerName })"
                          >
                            Remove
                          </button>
                        }
                      </li>
                    }
                  </ul>
                </details>
              </li>
            }
          </ul>

          @if (list.total > list.pageSize) {
            <app-pager
              [page]="list.page"
              [pageSize]="list.pageSize"
              [total]="list.total"
              noun="workers"
              (pageChange)="goToPage($event)"
            />
          }

          @if (cleanCount() > 0 && !list.projectArchived) {
            <div class="form__actions form__actions--sticky review-confirm">
              <span>{{ workers(cleanCount()) }} ready to confirm</span>
              <button
                appButton
                variant="primary"
                type="button"
                [busy]="confirming() === 'all'"
                (click)="confirmAll()"
              >
                Confirm all clean rows
              </button>
            </div>
          }
        }
      }
    </div>

    @if (adding(); as row) {
      <app-add-scan-dialog
        [projectId]="projectId()!"
        [workerId]="row.workerId"
        [workerName]="row.workerName"
        [workDate]="day()!.workDate"
        [suggested]="suggested(row)"
        (added)="changed('Scan added.', 'adding')"
        (dismissed)="adding.set(null)"
        (reloaded)="adding.set(null); reload()"
      />
    }
    @if (removing(); as target) {
      <app-remove-scan-dialog
        [punch]="target.punch"
        [workerName]="target.name"
        (removed)="changed('Scan removed.', 'removing')"
        (dismissed)="removing.set(null)"
        (reloaded)="removing.set(null); reload()"
      />
    }
    @if (importing() && day(); as list) {
      <app-import-scans-dialog
        [projectId]="list.projectId"
        [projectName]="list.projectName"
        (finished)="importing.set(false); reload()"
        (dismissed)="importing.set(false)"
        (reloaded)="importing.set(false); reload()"
      />
    }
  `,
  styles: `
    .review-bar {
      display: grid;
      gap: var(--space-3);
    }

    .review-rows {
      display: grid;
      gap: var(--space-3);
      padding: 0;
      list-style: none;
    }

    .review-row {
      display: grid;
      gap: var(--space-2);
    }

    .review-row--done {
      color: var(--color-text-muted);
    }

    .review-row__head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--space-2);
    }

    .review-row__name {
      font-size: var(--text-lg);
      margin-right: auto;
    }

    .review-row__hours {
      font-weight: 600;
    }

    .review-row__actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-2);
    }

    .review-muted {
      color: var(--color-text-muted);
    }

    .review-scans ul {
      display: grid;
      gap: var(--space-1);
      padding: 0;
      margin-top: var(--space-2);
      list-style: none;
    }

    .review-scans li {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--space-2);
    }

    .review-scans summary {
      min-height: var(--touch-target);
      display: flex;
      align-items: center;
      cursor: pointer;
    }

    .review-skip {
      display: block;
    }

    .review-confirm {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--space-2);
    }

    @media (min-width: 640px) {
      .review-bar {
        grid-template-columns: minmax(0, 2fr) minmax(0, 1fr);
        align-items: end;
      }
    }
  `,
})
export class TimeClockReviewPage {
  /** From the address: `/time-clock/<project>` or `?projectId=`. */
  readonly projectId = input<string | undefined>(undefined);
  readonly date = input<string | undefined>(undefined);
  readonly page = input(1, { transform: (value: unknown) => numberAttribute(value, 1) });

  private readonly api = inject(TimeClockApi);
  private readonly router = inject(Router);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);

  protected readonly chips = EXCEPTION_CHIPS;
  protected readonly formatDate = formatDate;
  protected readonly clockTime = clockTime;
  protected readonly directionWords = directionWords;
  protected readonly hoursLine = hoursLine;
  protected readonly hoursText = hoursText;
  protected readonly scanLine = scanLine;
  protected readonly workers = workers;

  protected readonly canManage = this.session.permit(Permission.timeClockManage);
  protected readonly today = computed(() => todayIn(this.session.timeZoneId()));

  private readonly projectList = rxResource({ stream: () => this.api.projects() });
  protected readonly projects = computed(() =>
    this.projectList.hasValue() ? this.projectList.value() : [],
  );

  protected readonly day = signal<TimeClockDayList | null>(null);
  protected readonly loading = signal(false);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly actionError = signal<ApiError | null>(null);
  protected readonly skipped = signal<ConfirmResult['skipped']>([]);
  protected readonly approved = signal<Readonly<Record<string, boolean>>>({});
  protected readonly confirming = signal<string | null>(null);
  protected readonly adding = signal<TimeClockDayRow | null>(null);
  protected readonly removing = signal<{ punch: TimeClockPunch; name: string } | null>(null);
  protected readonly importing = signal(false);

  /** Rows "Confirm all clean rows" will take: no exception, and any overtime approved. The server decides again when it confirms. */
  protected readonly cleanCount = computed(
    () =>
      (this.day()?.items ?? []).filter(
        (row) =>
          row.canConfirm &&
          row.exceptions.length === 0 &&
          (row.proposedOvertimeHours === 0 || this.approved()[row.workerId] === true),
      ).length,
  );

  /** The earliest day still waiting, when it isn't the one shown. */
  protected readonly waitingElsewhere = computed(() => {
    const list = this.day();
    const earliest = list?.summary.earliestDate ?? null;
    return earliest && earliest !== list?.workDate ? earliest : null;
  });

  protected readonly waitingTitle = computed(() => {
    const summary = this.day()?.summary;
    if (!summary) return '';
    const days = summary.daysWaiting === 1 ? '1 day' : `${summary.daysWaiting} days`;
    return `Scans wait for review on ${days}, from ${formatDate(summary.earliestDate)}`;
  });

  constructor() {
    effect(() => {
      const projectId = this.projectId();
      const date = this.date() ?? null;
      const page = this.page();
      untracked(() => {
        this.approved.set({});
        this.skipped.set([]);
        if (projectId) void this.load(projectId, date, page);
        else this.day.set(null);
      });
    });
  }

  protected reload(): void {
    const projectId = this.projectId();
    if (projectId)
      void this.load(projectId, this.date() ?? this.day()?.workDate ?? null, this.page());
  }

  protected chooseProject(event: Event): void {
    const id = (event.target as HTMLSelectElement).value;
    if (id) void this.router.navigate(['/time-clock', id]);
  }

  protected chooseDate(event: Event): void {
    const date = (event.target as HTMLInputElement).value;
    if (date) this.goTo(date);
  }

  protected goTo(date: string): void {
    void this.router.navigate([], {
      queryParams: { date, page: null },
      queryParamsHandling: 'merge',
    });
  }

  protected goToPage(page: number): void {
    void this.router.navigate([], { queryParams: { page }, queryParamsHandling: 'merge' });
  }

  protected approve(workerId: string, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.approved.update((current) => ({ ...current, [workerId]: checked }));
  }

  protected suggested(row: TimeClockDayRow): PunchDirection | null {
    if (row.exceptions.includes('MissingTimeOut')) return 'Out';
    if (row.exceptions.includes('MissingTimeIn')) return 'In';
    return null;
  }

  protected source(punch: TimeClockPunch): string {
    if (punch.source === 'Manual')
      return punch.reason ? `Added by a supervisor: ${punch.reason}` : 'Added by a supervisor';
    if (punch.source === 'Import') return 'From an imported file';
    return punch.deviceName ?? '';
  }

  protected async confirmRow(row: TimeClockDayRow): Promise<void> {
    await this.confirm(
      row.workerId,
      [{ workerId: row.workerId, approveOvertime: this.approved()[row.workerId] === true }],
      false,
    );
  }

  protected async confirmAll(): Promise<void> {
    const approvals = Object.entries(this.approved())
      .filter(([, approved]) => approved)
      .map(([workerId]) => ({ workerId, approveOvertime: true }));
    await this.confirm('all', approvals, true);
  }

  protected changed(message: string, dialog: 'adding' | 'removing'): void {
    if (dialog === 'adding') this.adding.set(null);
    else this.removing.set(null);
    this.toast.success(message);
    this.reload();
  }

  private async confirm(
    busy: string,
    rows: { workerId: string; approveOvertime: boolean }[],
    allClean: boolean,
  ): Promise<void> {
    const list = this.day();
    if (!list || this.confirming()) return;
    this.confirming.set(busy);
    this.actionError.set(null);
    this.skipped.set([]);
    try {
      const result = await firstValueFrom(
        this.api.confirm({ projectId: list.projectId, workDate: list.workDate, rows, allClean }),
      );
      if (result.confirmed.length > 0)
        this.toast.success(
          `Confirmed ${workers(result.confirmed.length)} for ${formatDate(list.workDate)}.`,
        );
      this.skipped.set(
        allClean
          ? result.skipped.filter((item) => item.reason !== 'Already recorded.')
          : result.skipped,
      );
      this.reload();
    } catch (failure) {
      this.actionError.set(asApiError(failure));
    } finally {
      this.confirming.set(null);
    }
  }

  private async load(projectId: string, date: string | null, page: number): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);
    try {
      this.day.set(await firstValueFrom(this.api.days(projectId, date, page)));
    } catch (failure) {
      this.day.set(null);
      this.loadError.set(asApiError(failure));
    } finally {
      this.loading.set(false);
    }
  }
}
