import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ToastService } from '../../../core/notifications/toast-service';
import { Permission } from '../../../core/session/permissions';
import { SessionState } from '../../../core/session/session-state';
import { ApiErrorAlert } from '../../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../../shared/ui/button/button';
import { EmptyState } from '../../../shared/ui/empty-state/empty-state';
import { Icon } from '../../../shared/ui/icon/icon';
import { Pager } from '../../../shared/ui/pager/pager';
import { Skeleton } from '../../../shared/ui/skeleton/skeleton';
import { ProjectContext } from '../../projects/project-context';
import { DiaryEntryDialog } from '../diary-entry-dialog';
import { DiaryFormDialog } from '../diary-form-dialog';
import { DiaryVoidDialog } from '../diary-void-dialog';
import { SiteDiaryListState } from '../site-diary-state';
import { dayBefore, headcountLine, reportDayLabel, weatherLabel } from '../site-diary-presentation';
import { DiaryEntry } from '../site-diary.models';

/**
 * A project's Diary tab: one report per day, newest first, each with the weather, how many workers were on site and the first line
 * of the work done. People who write reports get one primary action, "Write today's report", until today has one; everyone else just
 * reads. An archived project's diary is read-only, with one line saying why. The attendance and deliveries of a day are filled in by
 * the server, never typed here.
 */
@Component({
  selector: 'app-project-diary-tab',
  imports: [
    ApiErrorAlert,
    Button,
    EmptyState,
    Icon,
    Pager,
    Skeleton,
    DiaryEntryDialog,
    DiaryFormDialog,
    DiaryVoidDialog,
  ],
  providers: [SiteDiaryListState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="diary" aria-labelledby="diary-heading">
      <div class="diary__head">
        <h2 id="diary-heading" class="diary__title">Site diary</h2>
        @if (showWriteToday()) {
          <button appButton variant="primary" type="button" (click)="write(state.today())">
            <app-icon name="plus" [size]="18" />
            Write today's report
          </button>
        }
      </div>

      @if (archivedNote()) {
        <p class="text-small text-muted">
          This project is archived, so its reports can be read but not written or changed.
        </p>
      }

      @if (state.error(); as problem) {
        <app-api-error-alert
          [error]="problem"
          [allowRetry]="true"
          reloadLabel="Try again"
          (reload)="state.reload()"
        />
      } @else if (state.loadingFirst()) {
        <app-skeleton [lines]="4" />
      } @else if (state.isEmpty()) {
        <app-empty-state
          icon="contract"
          title="No site reports yet"
          [message]="emptyMessage()"
        >
          @if (canWrite()) {
            <button appButton variant="primary" type="button" (click)="write(state.today())">
              <app-icon name="plus" [size]="18" />
              Write today's report
            </button>
          }
        </app-empty-state>
      } @else {
        <ul class="card-list" [class.diary__list--busy]="state.refreshing()">
          @for (item of state.items(); track item.id) {
            <li>
              <button type="button" class="row-card diary-row" (click)="viewing.set(item.reportDate)">
                <span class="diary-row__top">
                  <span class="diary-row__date">{{ dayLabel(item.reportDate) }}</span>
                  <span class="chip">{{ weather(item.weather) }}</span>
                </span>
                <span class="diary-row__crew">{{ crew(item.workersOnSite, item.subcontractorWorkers) }}</span>
                <span class="diary-row__work">{{ item.workDoneFirstLine }}</span>
              </button>
            </li>
          }
        </ul>
        <app-pager
          [page]="state.page()"
          [pageSize]="state.pageSize"
          [total]="state.total()"
          noun="reports"
          (pageChange)="state.query.setPage($event)"
        />
        @if (canWrite() && !state.isEmpty()) {
          <p>
            <button appButton variant="ghost" type="button" (click)="write(earlierDay())">
              Write a report for an earlier day
            </button>
          </p>
        }
      }
    </section>

    @if (viewing(); as date) {
      <app-diary-entry-dialog
        [projectId]="projectId()"
        [reportDate]="date"
        (dismissed)="viewing.set(null)"
        (correct)="startCorrecting($event)"
        (voidIt)="startVoiding($event)"
      />
    }

    @if (writing(); as date) {
      <app-diary-form-dialog
        [projectId]="projectId()"
        [initialDate]="date"
        (saved)="written('Report saved.')"
        (dismissed)="writing.set(null)"
        (reloaded)="closeAndReload()"
      />
    }

    @if (correcting(); as entry) {
      <app-diary-form-dialog
        [projectId]="projectId()"
        [entry]="entry"
        (saved)="written('Report updated.')"
        (dismissed)="correcting.set(null)"
        (reloaded)="closeAndReload()"
      />
    }

    @if (voiding(); as entry) {
      <app-diary-void-dialog
        [projectId]="projectId()"
        [entry]="entry"
        (voided)="written('Report voided. You can write a new one for that day.')"
        (dismissed)="voiding.set(null)"
        (reloaded)="closeAndReload()"
      />
    }
  `,
  styles: `
    :host {
      display: block;
    }

    .diary {
      display: grid;
      gap: var(--space-4);
    }

    .diary__head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--space-2) var(--space-3);
    }

    .diary__title {
      font-size: var(--text-lg);
    }

    .diary__list--busy {
      opacity: 0.6;
    }

    .diary-row {
      width: 100%;
      min-height: var(--touch-target);
      text-align: left;
      font: inherit;
      color: inherit;
      cursor: pointer;
    }

    .diary-row:hover {
      border-color: var(--color-border-strong);
    }

    .diary-row:focus-visible {
      outline: var(--focus-ring-width) solid var(--color-focus);
      outline-offset: 2px;
    }

    .diary-row__top {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--space-3);
    }

    .diary-row__date {
      font-weight: 650;
    }

    .diary-row__crew {
      font-size: var(--text-sm);
      color: var(--color-text-muted);
    }

    .diary-row__work {
      display: -webkit-box;
      -webkit-line-clamp: 2;
      line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
      overflow-wrap: anywhere;
    }
  `,
})
export class ProjectDiaryTab {
  protected readonly state = inject(SiteDiaryListState);
  private readonly context = inject(ProjectContext);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);

  protected readonly projectId = computed(() => this.context.projectId() ?? '');
  private readonly mayWrite = this.session.permit(Permission.diaryWrite);
  protected readonly canWrite = computed(() => this.mayWrite() && !this.context.isArchived());
  /** Says why writing is missing, to the people who would otherwise have it. */
  protected readonly archivedNote = computed(() => this.mayWrite() && this.context.isArchived());

  /** The one primary action: today's report, until it exists. Only the first page can tell, because the newest reports are on it. */
  protected readonly showWriteToday = computed(
    () =>
      this.canWrite() &&
      !this.state.isEmpty() &&
      this.state.result() !== undefined &&
      this.state.page() === 1 &&
      !this.state.hasToday(),
  );

  protected readonly emptyMessage = computed(() =>
    this.canWrite()
      ? 'A report records the weather, the work done and who was on site, one day at a time. Attendance and deliveries are filled in for you.'
      : 'Reports for this project will appear here once someone writes them.',
  );

  protected readonly earlierDay = computed(() => dayBefore(this.state.today()));

  protected readonly viewing = signal<string | null>(null);
  protected readonly writing = signal<string | null>(null);
  protected readonly correcting = signal<DiaryEntry | null>(null);
  protected readonly voiding = signal<DiaryEntry | null>(null);

  protected readonly dayLabel = reportDayLabel;
  protected readonly weather = weatherLabel;
  protected readonly crew = headcountLine;

  protected write(date: string): void {
    this.writing.set(date);
  }

  protected startCorrecting(entry: DiaryEntry): void {
    this.viewing.set(null);
    this.correcting.set(entry);
  }

  protected startVoiding(entry: DiaryEntry): void {
    this.viewing.set(null);
    this.voiding.set(entry);
  }

  /** A report was saved, corrected or voided: close whatever was open and show the list as it now is. */
  protected written(message: string): void {
    this.writing.set(null);
    this.correcting.set(null);
    this.voiding.set(null);
    this.toast.success(message);
    if (this.state.page() > 1) this.state.query.setPage(1);
    else this.state.reload();
  }

  /** The person chose to reload after a refusal: the list is read again. */
  protected closeAndReload(): void {
    this.writing.set(null);
    this.correcting.set(null);
    this.voiding.set(null);
    this.state.reload();
  }
}
