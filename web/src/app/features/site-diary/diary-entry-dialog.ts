import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { asApiError } from '../../core/api/api-error';
import { SessionState } from '../../core/session/session-state';
import { Permission } from '../../core/session/permissions';
import { formatDate, formatDateTime, todayIn } from '../../shared/presentation/business-time';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { DiaryFilledInPanels } from './diary-filled-in';
import { SiteDiaryApi } from './site-diary-api';
import { reportDayLabel, weatherLabel, workersWords } from './site-diary-presentation';
import { DiaryEntry } from './site-diary.models';

/**
 * One day's report, read-only: the weather, the work done, anything on issues, visitors and safety, the subcontractor workers, and
 * who was on site and what was delivered. The person who may change it sees "Correct report" and "Void report"; anyone else who
 * writes reports is told, in one line, who can. The report is read when the dialog opens, so it is always the saved one.
 */
@Component({
  selector: 'app-diary-entry-dialog',
  imports: [Dialog, Button, Skeleton, ApiErrorAlert, DiaryFilledInPanels],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog [title]="title()" variant="sheet" (closed)="dismissed.emit()">
      <div class="stack">
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [allowRetry]="true"
            reloadLabel="Try again"
            (reload)="resource.reload()"
          />
        } @else if (entry(); as report) {
          <p class="meta">
            <span class="chip">{{ weather(report.weather) }}</span>
            @if (report.weatherNote) {
              <span class="text-muted">{{ report.weatherNote }}</span>
            }
          </p>
          <section>
            <h3 class="heading">Work done</h3>
            <p class="text">{{ report.workDone }}</p>
          </section>
          @if (report.issuesAndDelays) {
            <section>
              <h3 class="heading">Issues and delays</h3>
              <p class="text">{{ report.issuesAndDelays }}</p>
            </section>
          }
          @if (report.visitorsAndInspections) {
            <section>
              <h3 class="heading">Visitors and inspections</h3>
              <p class="text">{{ report.visitorsAndInspections }}</p>
            </section>
          }
          @if (report.safetyNotes) {
            <section>
              <h3 class="heading">Safety notes</h3>
              <p class="text">{{ report.safetyNotes }}</p>
            </section>
          }
          <p>Subcontractor workers on site: {{ report.subcontractorWorkers }}</p>
          <app-diary-filled-in [filledIn]="report" [date]="report.reportDate" [today]="today()" />
          <p class="text-small text-muted">
            @if (report.authorName) {
              Written by {{ report.authorName }}.
            }
            @if (changedLater(report)) {
              Last changed {{ changedAt(report.updatedAtUtc) }}.
            }
          </p>
          @if (report.canChange) {
            <p class="text-small text-muted">{{ whoCanChange(report) }}</p>
          } @else if (canWrite() && !report.projectArchived) {
            <p class="text-small text-muted">
              Only the person who wrote this report can change it, until the end of
              {{ date(report.changeableUntil) }}. A Project Manager or an Administrator can change it at any time.
            </p>
          } @else if (report.projectArchived) {
            <p class="text-small text-muted">This project is archived, so its reports can't be changed.</p>
          }
        } @else {
          <app-skeleton [lines]="4" />
        }
      </div>
      <div dialogActions class="dialog-actions">
        <button appButton type="button" (click)="dismissed.emit()">Close</button>
        @if (entry(); as report) {
          @if (report.canChange) {
            <button appButton variant="danger" type="button" (click)="voidIt.emit(report)">
              Void report
            </button>
            <button appButton variant="primary" type="button" (click)="correct.emit(report)">
              Correct report
            </button>
          }
        }
      </div>
    </app-dialog>
  `,
  styles: `
    .meta {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--space-2);
    }

    .heading {
      font-size: var(--text-base);
      margin-bottom: var(--space-1);
    }

    .text {
      white-space: pre-wrap;
      overflow-wrap: anywhere;
    }
  `,
})
export class DiaryEntryDialog {
  readonly projectId = input.required<string>();
  /** The day of the report to show, "2026-11-02". */
  readonly reportDate = input.required<string>();

  readonly dismissed = output<void>();
  readonly correct = output<DiaryEntry>();
  readonly voidIt = output<DiaryEntry>();

  private readonly api = inject(SiteDiaryApi);
  private readonly session = inject(SessionState);

  protected readonly canWrite = this.session.permit(Permission.diaryWrite);
  protected readonly today = computed(() => todayIn(this.session.timeZoneId()));

  protected readonly resource = rxResource({
    params: () => ({ projectId: this.projectId(), date: this.reportDate() }),
    stream: ({ params }) => this.api.get(params.projectId, params.date),
  });
  protected readonly entry = computed(() => (this.resource.hasValue() ? this.resource.value() : undefined));
  protected readonly error = computed(() => {
    const failure = this.resource.error();
    return failure ? asApiError(failure) : null;
  });

  protected readonly title = computed(() => reportDayLabel(this.reportDate()));
  protected readonly weather = weatherLabel;
  protected readonly date = formatDate;
  protected readonly workers = workersWords;

  protected changedAt(instant: string): string {
    return formatDateTime(instant, this.session.timeZoneId());
  }

  /** The report was changed after it was written (a correction): say so. */
  protected changedLater(report: DiaryEntry): boolean {
    return report.version > 0;
  }

  /** When the author's own time to change it runs out, and who is not limited by it. */
  protected whoCanChange(report: DiaryEntry): string {
    return `The person who wrote this report can change it until the end of ${formatDate(report.changeableUntil)}. A Project Manager or an Administrator can change it at any time.`;
  }
}
