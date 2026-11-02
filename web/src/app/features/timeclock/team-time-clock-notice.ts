import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { of } from 'rxjs';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { formatDate } from '../../shared/presentation/business-time';
import { Alert } from '../../shared/ui/alert/alert';
import { Button } from '../../shared/ui/button/button';
import { TimeClockApi } from './timeclock-api';

/**
 * On a project's Team tab: when days of time clock scans wait for review, one line says so and leads to them. It shows nothing when nothing waits,
 * and nothing to people who don't confirm scans (it isn't even asked for).
 */
@Component({
  selector: 'app-team-time-clock-notice',
  imports: [RouterLink, Alert, Button],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (waiting(); as summary) {
      <app-alert tone="info" [title]="title()">
        <div alertActions>
          <a appButton [routerLink]="['/time-clock', projectId()]">Review time clock scans</a>
        </div>
      </app-alert>
    }
  `,
})
export class TeamTimeClockNotice {
  readonly projectId = input.required<string>();

  private readonly api = inject(TimeClockApi);
  private readonly session = inject(SessionState);

  private readonly summary = rxResource({
    params: () => ({
      projectId: this.projectId(),
      allowed: this.session.can(Permission.timeClockReview),
    }),
    stream: ({ params }) => (params.allowed ? this.api.summary(params.projectId) : of(null)),
  });

  protected readonly waiting = computed(() => {
    const summary = this.summary.hasValue() ? this.summary.value() : null;
    return summary && summary.rowsWaiting > 0 ? summary : null;
  });

  protected readonly title = computed(() => {
    const summary = this.waiting();
    if (!summary) return '';
    const days = summary.daysWaiting === 1 ? '1 day' : `${summary.daysWaiting} days`;
    return `Time clock scans wait for review on ${days}, from ${formatDate(summary.earliestDate)}`;
  });
}
