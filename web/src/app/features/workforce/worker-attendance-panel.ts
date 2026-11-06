import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { formatDate } from '../../shared/presentation/business-time';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { dayTypeChip, hoursWords } from './attendance-sheet';
import { WorkerDetailState } from './worker-detail-state';
import { WorkforceApi } from './workforce-api';

/** How many days of attendance the profile shows: enough to see the week, not a whole history. */
export const RECENT_ATTENDANCE_COUNT = 7;

/** The worker's latest attendance, newest day first: date, project and hours. Attendance never carries pay. */
@Component({
  selector: 'app-worker-attendance-panel',
  imports: [RouterLink, StatusChip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (rows().length > 0) {
      <section class="card" aria-labelledby="attendance-heading">
        <h2 id="attendance-heading" class="card__title">Recent attendance</h2>
        <ul class="days">
          @for (row of rows(); track row.id) {
            <li class="days__row">
              <span class="days__date">{{ row.date }}</span>
              <a class="days__project" [routerLink]="['/projects', row.projectId, 'team']">{{
                row.project
              }}</a>
              <span class="days__hours">{{ row.hours }}</span>
              @if (row.day; as day) {
                <app-status-chip [label]="day.label" [tone]="day.tone" [icon]="day.icon" />
              }
              @if (row.locked) {
                <app-status-chip label="In a payroll run" tone="neutral" icon="lock" />
              }
            </li>
          }
        </ul>
      </section>
    }
  `,
  styles: `
    :host {
      display: block;
    }

    :host:empty {
      display: none;
    }

    .days {
      display: grid;
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .days__row {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto;
      gap: 2px var(--space-3);
      align-items: center;
      padding: var(--space-3) 0;
      border-bottom: 1px solid var(--color-border);
    }

    .days__row:last-child {
      border-bottom: 0;
    }

    .days__date {
      font-weight: 650;
    }

    .days__hours {
      grid-column: 2;
      grid-row: 1;
      text-align: right;
      font-variant-numeric: tabular-nums;
    }

    // The link keeps a 44px target without making the row taller.
    .days__project {
      grid-column: 1;
      justify-self: start;
      padding: 12px 0;
      margin: -12px 0;
      color: var(--color-text-muted);
      font-size: var(--text-sm);
    }

    .days__row app-status-chip {
      grid-column: 2;
      justify-self: end;
    }

    .days__row app-status-chip + app-status-chip {
      grid-column: 1 / -1;
      justify-self: end;
    }

    @media (min-width: 48rem) {
      .days__row {
        grid-template-columns: 9rem minmax(0, 1fr) auto auto;
      }

      .days__hours,
      .days__project,
      .days__row app-status-chip {
        grid-column: auto;
        grid-row: auto;
      }
    }
  `,
})
export class WorkerAttendancePanel {
  private readonly api = inject(WorkforceApi);
  private readonly state = inject(WorkerDetailState);

  private readonly resource = rxResource({
    params: () => this.state.worker().id,
    stream: ({ params }) => this.api.recentAttendance(params, RECENT_ATTENDANCE_COUNT),
  });

  protected readonly rows = computed(() => {
    if (!this.resource.hasValue()) return [];
    return this.resource.value().items.map((entry) => ({
      id: entry.id,
      date: formatDate(entry.workDate),
      project: entry.subProjectName
        ? `${entry.projectName}, ${entry.subProjectName}`
        : entry.projectName,
      projectId: entry.projectId,
      hours: hoursWords(entry.regularHours, entry.overtimeHours, entry.nightHours),
      day: dayTypeChip(entry.dayType),
      locked: entry.isLocked,
    }));
  });
}
