import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { WorkerDetailState } from '../workforce/worker-detail-state';
import { groupedCode } from './badge-card';
import { TimeClockApi } from './timeclock-api';

/**
 * The worker's time clock badge, on the worker's page, for people who issue badges (timeclock.manage). "Issue badge code" gives an active worker
 * their first code; "Reissue badge code" replaces it after a confirmation that says the old badge stops working at once. The new code is shown here
 * once, with a link to print the badge.
 */
@Component({
  selector: 'app-worker-badge-panel',
  imports: [RouterLink, Button, Dialog, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (shown()) {
      <section class="card stack" aria-labelledby="badge-heading">
        <div class="panel-head">
          <h2 id="badge-heading" class="card__title">Time clock badge</h2>
          @if (hasCode()) {
            <button appButton type="button" (click)="confirming.set(true)">
              Reissue badge code
            </button>
          } @else {
            <button appButton type="button" [busy]="saving()" (click)="issue()">
              Issue badge code
            </button>
          }
        </div>
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            reloadLabel="Reload the worker"
            (reload)="state.reload()"
          />
        }
        @if (issuedCode(); as code) {
          <p>
            New code: <strong class="badge-code">{{ code }}</strong
            >.
            <a [routerLink]="['/time-clock/badges']" [queryParams]="{ workers: state.worker().id }"
              >Print the badge</a
            >
          </p>
        } @else if (hasCode()) {
          <p>
            {{ state.worker().firstName }} has a badge. A lost badge is replaced with "Reissue badge
            code".
          </p>
        } @else {
          <p>
            No badge yet. Issue a code to print a badge {{ state.worker().firstName }} can scan at
            the time clock.
          </p>
        }
      </section>
    }

    @if (confirming()) {
      <app-dialog title="Reissue badge code?" [locked]="saving()" (closed)="confirming.set(false)">
        <p>
          {{ state.worker().fullName }}'s current badge stops working at once. Print the new badge
          and give it to them before their next shift.
        </p>
        <div dialogActions class="dialog-actions">
          <button
            appButton
            type="button"
            [attr.disabled]="saving() ? '' : null"
            (click)="confirming.set(false)"
          >
            Keep the current badge
          </button>
          <button appButton variant="danger" type="button" [busy]="saving()" (click)="issue()">
            Reissue badge code
          </button>
        </div>
      </app-dialog>
    }
  `,
  styles: `
    .badge-code {
      font-family: ui-monospace, monospace;
      letter-spacing: 0.06em;
    }
  `,
})
export class WorkerBadgePanel {
  protected readonly state = inject(WorkerDetailState);
  private readonly api = inject(TimeClockApi);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);

  protected readonly saving = signal(false);
  protected readonly confirming = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly issuedCode = signal<string | null>(null);
  private readonly issued = signal(false);

  /** Only for people who issue badges, and only for an active worker (a badge is for coming to work). */
  protected readonly shown = computed(
    () =>
      this.session.can(Permission.timeClockManage) &&
      this.state.status() === 'ready' &&
      this.state.worker().status === 'Active',
  );

  protected readonly hasCode = computed(
    () => this.issued() || this.state.worker().hasPunchCode === true,
  );

  protected async issue(): Promise<void> {
    if (this.saving()) return;
    this.saving.set(true);
    this.error.set(null);
    try {
      const result = await firstValueFrom(
        this.api.issueCode(this.state.worker().id, this.state.worker().version),
      );
      this.issuedCode.set(groupedCode(result.code));
      this.issued.set(true);
      this.confirming.set(false);
      this.toast.success(
        result.reissued
          ? 'New badge code issued. The old badge no longer works.'
          : 'Badge code issued.',
      );
      await this.state.reload();
    } catch (failure) {
      this.error.set(asApiError(failure));
      this.confirming.set(false);
    } finally {
      this.saving.set(false);
    }
  }
}
