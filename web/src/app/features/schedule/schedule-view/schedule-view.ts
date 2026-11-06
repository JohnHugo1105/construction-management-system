import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ApiError, asApiError } from '../../../core/api/api-error';
import { ToastService } from '../../../core/notifications/toast-service';
import { Permission } from '../../../core/session/permissions';
import { SessionState } from '../../../core/session/session-state';
import { formatDate, formatDateTime } from '../../../shared/presentation/business-time';
import { formatMoney } from '../../../shared/presentation/money';
import { formatBps } from '../../../shared/presentation/percent';
import { ApiErrorAlert } from '../../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../../shared/ui/button/button';
import { ConfirmDialog } from '../../../shared/ui/dialog/confirm-dialog';
import { EmptyState } from '../../../shared/ui/empty-state/empty-state';
import { Icon } from '../../../shared/ui/icon/icon';
import { Menu } from '../../../shared/ui/menu/menu';
import { Skeleton } from '../../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../../shared/ui/status-chip/status-chip';
import { ReasonDialog } from '../../costing/ui/reason-dialog';
import { ProjectContext } from '../../projects/project-context';
import { ActivityDialog } from '../activity-dialog';
import { SCurveChart } from '../s-curve-chart';
import {
  activityStatus,
  barPosition,
  behindText,
  datesText,
  linesText,
  slipText,
  timelineSpan,
  todayPosition,
} from '../schedule-presentation';
import { ScheduleState } from '../schedule-state';
import { ScheduleActivity } from '../schedule.models';

/** Activities drawn at first; "Show more activities" adds this many again, so a long plan stays light. */
const ACTIVITIES_PER_STEP = 30;

/**
 * The Schedule view of the Progress tab: the project's planned activities with their dates, weight (the budget of the lines they
 * cover), percent complete and state, a timeline on wider screens, and the S-curve of planned against actual. People with
 * `schedule.write` add, change and remove activities and set the baseline; everyone else reads it. An archived project's schedule
 * is read-only. The server works out every figure; this view only shows them.
 */
@Component({
  selector: 'app-schedule-view',
  imports: [
    ActivityDialog,
    ApiErrorAlert,
    Button,
    ConfirmDialog,
    EmptyState,
    Icon,
    Menu,
    ReasonDialog,
    SCurveChart,
    Skeleton,
    StatusChip,
  ],
  providers: [ScheduleState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './schedule-view.html',
  styleUrl: './schedule-view.scss',
})
export class ScheduleView {
  protected readonly state = inject(ScheduleState);
  private readonly context = inject(ProjectContext);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);

  private readonly mayWrite = this.session.permit(Permission.scheduleWrite);
  private readonly archived = computed(
    () => this.context.isArchived() || (this.state.schedule()?.projectArchived ?? false),
  );
  protected readonly canWrite = computed(() => this.mayWrite() && !this.archived());
  /** Says why planning is missing, to the people who would otherwise have it. */
  protected readonly archivedNote = computed(() => this.mayWrite() && this.archived());
  protected readonly currency = this.session.currencyCode;

  /** Nothing can be weighted until the project has a budget. */
  protected readonly noBudget = computed(() => {
    const schedule = this.state.schedule();
    return (
      schedule !== undefined &&
      schedule.budgetLines.length === 0 &&
      schedule.activities.length === 0
    );
  });

  protected readonly summary = computed(() => {
    const schedule = this.state.schedule();
    if (!schedule || schedule.activities.length === 0) return null;
    const currency = this.currency();
    const lines = schedule.unplannedLineCount;
    return {
      planned: schedule.usesSchedule
        ? `${formatMoney(schedule.plannedMinor, currency)} of work planned by today, of ${formatMoney(schedule.budgetMinor, currency)} budgeted`
        : '',
      behind: behindText(schedule.activities),
      baseline: schedule.baselineSetAtUtc
        ? `Baseline set ${formatDateTime(schedule.baselineSetAtUtc, this.session.timeZoneId())}`
        : '',
      unplanned:
        schedule.usesSchedule && lines > 0
          ? `${lines} budget ${lines === 1 ? 'line' : 'lines'} (${formatMoney(schedule.unplannedBudgetMinor, currency)}) ${lines === 1 ? 'is' : 'are'} not in any activity, so ${lines === 1 ? 'it is' : 'they are'} planned evenly over the project's dates.`
          : '',
      notInUse: schedule.usesSchedule
        ? ''
        : schedule.planWeightMinor > 0
          ? "Some budget lines are in no activity and the project has no end date, so they can't be planned yet. Set the project's end date or put every budget line in an activity, and the Progress tab will plan with this schedule."
          : "No activity has budget lines yet, so the Progress tab still plans the work in a straight line between the project's start and end dates. Link budget lines to an activity to plan with this schedule.",
    };
  });

  private readonly span = computed(() => timelineSpan(this.state.activities()));

  private readonly limit = signal(ACTIVITIES_PER_STEP);

  protected readonly rows = computed(() => {
    const schedule = this.state.schedule();
    if (!schedule) return [];
    const span = this.span();
    const lines = this.state.lineById();
    return schedule.activities.slice(0, this.limit()).map((activity) => ({
      activity,
      status: activityStatus(activity),
      // One line, so a screen reader hears it with its separators: dates, percent complete, budget.
      meta: [
        datesText(activity),
        activity.percentCompleteBps === null
          ? ''
          : `${formatBps(activity.percentCompleteBps)} complete`,
        activity.needsLines ? '' : `${formatMoney(activity.weightMinor, this.currency())} budget`,
      ]
        .filter((part) => part !== '')
        .join(' · '),
      slip: slipText(activity.slipDays),
      lines: linesText(activity, lines),
      baselineDates:
        activity.baselineStart && activity.baselineFinish
          ? `Baseline ${formatDate(activity.baselineStart)} to ${formatDate(activity.baselineFinish)}`
          : '',
      bar: span ? barPosition(activity.plannedStart, activity.plannedFinish, span) : null,
      baselineBar:
        span && activity.baselineStart && activity.baselineFinish
          ? barPosition(activity.baselineStart, activity.baselineFinish, span)
          : null,
      fill: Math.min(100, (activity.percentCompleteBps ?? 0) / 100),
    }));
  });

  protected readonly hiddenCount = computed(() =>
    Math.max(0, this.state.activities().length - this.limit()),
  );

  protected readonly todayAt = computed(() => {
    const schedule = this.state.schedule();
    const span = this.span();
    return schedule && span ? todayPosition(schedule.today, span) : null;
  });

  // ---- Dialogs --------------------------------------------------------------------------------------------------------------

  /** The activity being changed, `'new'` while adding one, or null. */
  protected readonly editing = signal<ScheduleActivity | 'new' | null>(null);
  protected readonly removing = signal<ScheduleActivity | null>(null);
  /** What the reason dialog runs: made once per activity, so the dialog's input stays the same while it is open. */
  protected readonly removeAction = computed(() => {
    const activity = this.removing();
    return activity ? this.state.remove(activity) : null;
  });
  protected readonly confirmingBaseline = signal(false);
  protected readonly settingBaseline = signal(false);
  protected readonly actionError = signal<ApiError | null>(null);

  protected readonly baselineMessage = computed(() => {
    const schedule = this.state.schedule();
    const count = schedule?.activities.length ?? 0;
    const what = `This saves the planned dates of all ${count} ${count === 1 ? 'activity' : 'activities'} as the baseline: the plan you measure against.`;
    const after = 'From then on, a moved date shows as days later or earlier than the baseline.';
    const again = schedule?.baselineSetAtUtc
      ? `It replaces the baseline set ${formatDateTime(schedule.baselineSetAtUtc, this.session.timeZoneId())}.`
      : 'You can set it again later; the new one replaces it.';
    return `${what} ${after} ${again}`;
  });

  protected showMore(): void {
    this.limit.set(this.limit() + ACTIVITIES_PER_STEP);
  }

  protected editTarget(): ScheduleActivity | null {
    const editing = this.editing();
    return editing === 'new' ? null : editing;
  }

  protected saved(): void {
    const adding = this.editing() === 'new';
    this.editing.set(null);
    this.actionError.set(null);
    this.toast.success(adding ? 'Activity added.' : 'Activity saved.');
  }

  protected removed(): void {
    this.removing.set(null);
    this.actionError.set(null);
    this.toast.success('Activity removed from the schedule.');
  }

  protected async setBaseline(): Promise<void> {
    if (this.settingBaseline()) return;
    this.settingBaseline.set(true);
    this.actionError.set(null);
    try {
      const schedule = await this.state.setBaseline();
      const count = schedule.activities.length;
      this.toast.success(`Baseline set for ${count} ${count === 1 ? 'activity' : 'activities'}.`);
    } catch (failure) {
      this.actionError.set(asApiError(failure));
    } finally {
      this.settingBaseline.set(false);
      this.confirmingBaseline.set(false);
    }
  }

  protected reload(): void {
    this.actionError.set(null);
    this.state.reload();
  }
}
