import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterNextRender,
  computed,
  inject,
  signal,
  Injector,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { ToastService } from '../../../core/notifications/toast-service';
import { Permission } from '../../../core/session/permissions';
import { SessionState } from '../../../core/session/session-state';
import { formatDate } from '../../../shared/presentation/business-time';
import { Alert } from '../../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../../shared/ui/button/button';
import { ConfirmDialog } from '../../../shared/ui/dialog/confirm-dialog';
import { EmptyState } from '../../../shared/ui/empty-state/empty-state';
import { Icon } from '../../../shared/ui/icon/icon';
import { Menu } from '../../../shared/ui/menu/menu';
import { Skeleton } from '../../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../../shared/ui/status-chip/status-chip';
import { Viewport } from '../../../shared/ui/viewport/viewport';
import { ProjectContext } from '../../projects/project-context';
import { TeamTimeClockNotice } from '../../timeclock/team-time-clock-notice';
import { AttendanceSheetState } from '../attendance-sheet-state';
import { dayTypeChip, holidayLine, hoursWords } from '../attendance-sheet';
import { HoursField } from '../hours-field';
import { VoidAttendanceDialog } from '../void-attendance-dialog';
import { AttendanceEntry } from '../workforce.models';

/**
 * The Team tab of a project: a daily attendance sheet for the crew posted here. People who may record attendance fill in a day
 * and save it in one step; everyone else sees who worked and for how long. An archived project is read-only. Hours only: pay
 * never appears here.
 */
@Component({
  selector: 'app-project-team-tab',
  imports: [
    RouterLink,
    Alert,
    ApiErrorAlert,
    Button,
    ConfirmDialog,
    EmptyState,
    HoursField,
    Icon,
    Menu,
    Skeleton,
    StatusChip,
    VoidAttendanceDialog,
    TeamTimeClockNotice,
  ],
  providers: [AttendanceSheetState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './project-team-tab.html',
  styleUrl: './project-team-tab.scss',
})
export class ProjectTeamTab {
  protected readonly state = inject(AttendanceSheetState);
  protected readonly context = inject(ProjectContext);
  protected readonly viewport = inject(Viewport);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  /** The person may record attendance, and the project still takes records. */
  protected readonly canRecord = computed(
    () => this.session.can(Permission.attendanceWrite) && !this.context.isArchived(),
  );
  protected readonly canManageWorkers = this.session.permit(Permission.workforceManage);

  protected readonly dayLabel = computed(() => formatDate(this.state.date()));
  protected readonly removing = signal<AttendanceEntry | null>(null);
  /** A day the person asked for while there were unsaved hours: held until they say whether to leave them. */
  protected readonly pendingDay = signal<{ date: string; input: HTMLInputElement | null } | null>(
    null,
  );

  protected readonly summary = computed(() => {
    const sheet = this.state.sheet();
    if (!sheet || sheet.entries.length === 0) return '';
    const count = sheet.entries.length === 1 ? '1 worker' : `${sheet.entries.length} workers`;
    return `${count} recorded: ${hoursWords(sheet.totalRegularHours, sheet.totalOvertimeHours)}`;
  });

  /** The saved entries, each with its words worked out so the template only prints them. */
  protected readonly entries = computed(() =>
    (this.state.sheet()?.entries ?? []).map((entry) => ({
      entry,
      hours: hoursWords(entry.regularHours, entry.overtimeHours, entry.nightHours),
      place: entry.subProjectName,
      editable: this.canRecord() && !entry.isLocked,
      // A rest day or holiday for this worker, as the server decided it; nothing on an ordinary day.
      day: dayTypeChip(entry.dayType),
    })),
  );

  /** The company holiday on the day, in one chip; nothing on an ordinary day. */
  protected readonly holiday = computed(() => {
    const sheet = this.state.sheet();
    return sheet ? holidayLine(sheet) : null;
  });
  protected readonly anyLocked = computed(() => this.entries().some((item) => item.entry.isLocked));

  protected readonly crewLabel = computed(() => {
    const count = this.state.rows().length;
    return count === 1 ? '1 worker not recorded yet' : `${count} workers not recorded yet`;
  });

  protected readonly saveSummary = computed(() => {
    const count = this.state.toRecord().length;
    const away = this.state.notWorking();
    const base = count === 1 ? '1 worker will be recorded' : `${count} workers will be recorded`;
    return away > 0 ? `${base}. ${away} marked as not working.` : `${base}.`;
  });

  protected dateChosen(event: Event): void {
    const input = event.target as HTMLInputElement;
    const date = input.value;
    if (date === '' || date === this.state.date()) return;
    if (this.state.edited() && !this.state.futureDate()) this.pendingDay.set({ date, input });
    else this.state.setDate(date);
  }

  protected discardAndOpen(): void {
    const pending = this.pendingDay();
    this.pendingDay.set(null);
    if (pending) this.state.setDate(pending.date);
  }

  protected keepEditing(): void {
    const pending = this.pendingDay();
    this.pendingDay.set(null);
    if (pending?.input) pending.input.value = this.state.date();
  }

  protected today(): void {
    const date = this.state.today();
    if (this.state.edited()) this.pendingDay.set({ date, input: null });
    else this.state.setDate(date);
  }

  protected subProjectChosen(event: Event): void {
    this.state.subProjectId.set((event.target as HTMLSelectElement).value);
  }

  protected async save(): Promise<void> {
    if (await this.state.saveDay()) this.toast.success(`Attendance saved for ${this.dayLabel()}.`);
    else this.showWhatNeedsAttention();
  }

  protected async saveEdit(): Promise<void> {
    if (await this.state.saveEdit()) this.toast.success('Hours changed.');
    else this.showWhatNeedsAttention();
  }

  /**
   * After a refused save, brings the first problem into view: the first box with a message (the keyboard lands in it), or the
   * alert above the sheet. With twenty workers on a phone the line at fault is otherwise far off screen.
   */
  private showWhatNeedsAttention(): void {
    afterNextRender(
      () => {
        const root = this.host.nativeElement;
        const box = root.querySelector<HTMLElement>('[aria-invalid="true"]');
        if (box) {
          box.focus();
          return;
        }
        // (Older engines and test DOMs have no scrollIntoView; there is nothing to do for them.)
        root
          .querySelector<HTMLElement>('.alert--danger, .alert--warning')
          ?.scrollIntoView?.({ block: 'center' });
      },
      { injector: this.injector },
    );
  }

  protected removed(): void {
    this.removing.set(null);
    this.state.reload();
    this.toast.success('Entry removed.');
  }

  protected checked(event: Event): boolean {
    return (event.target as HTMLInputElement).checked;
  }

  protected remarksTyped(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }
}
