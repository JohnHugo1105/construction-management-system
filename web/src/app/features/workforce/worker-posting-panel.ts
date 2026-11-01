import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ToastService } from '../../core/notifications/toast-service';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { formatDate } from '../../shared/presentation/business-time';
import { Button } from '../../shared/ui/button/button';
import { AssignDialog } from './assign-dialog';
import { ReleaseDialog } from './release-dialog';
import { WorkerDetailState } from './worker-detail-state';
import { WorkerAssignment, WorkerDetail } from './workforce.models';

function placeOf(assignment: WorkerAssignment): string {
  return assignment.subProjectName
    ? `${assignment.projectName}, ${assignment.subProjectName}`
    : assignment.projectName;
}

/**
 * Where the worker is posted now, with the two things a person does about it (post them somewhere, or release them) and the
 * earlier postings. Posting needs an active worker; the API's own messages explain anything it refuses.
 */
@Component({
  selector: 'app-worker-posting-panel',
  imports: [Button, AssignDialog, ReleaseDialog],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="card" aria-labelledby="posting-heading">
      <h2 id="posting-heading" class="card__title">Project</h2>

      @if (current()) {
        <p class="posting__place">{{ place() }}</p>
        <p class="text-muted text-small">Since {{ since() }}</p>
      } @else {
        <p class="text-muted">Not posted to a project.</p>
      }

      @if (canManage() && !left()) {
        @if (!active()) {
          <p class="text-muted text-small posting__note">
            Only an active worker can be posted to a project. Change their status first.
          </p>
        } @else {
          <div class="posting__actions">
            <button
              appButton
              type="button"
              [variant]="current() ? 'secondary' : 'primary'"
              (click)="assigning.set(true)"
            >
              {{ current() ? 'Move to another project' : 'Assign to a project' }}
            </button>
            @if (current()) {
              <button appButton type="button" (click)="releasing.set(true)">
                Release from the project
              </button>
            }
          </div>
        }
      }

      @if (earlier().length > 0) {
        <details class="posting__history">
          <summary>Earlier projects ({{ earlier().length }})</summary>
          <ul class="history">
            @for (item of earlier(); track item.id) {
              <li>
                <span class="history__what">{{ item.place }}</span>
                <span class="text-muted text-small">{{ item.dates }}</span>
              </li>
            }
          </ul>
        </details>
      }
    </section>

    @if (assigning()) {
      <app-assign-dialog
        [workers]="[{ id: worker().id, version: worker().version }]"
        [summary]="worker().fullName"
        (assigned)="assigned($event)"
        (dismissed)="assigning.set(false)"
        (reloaded)="reload()"
      />
    }
    @if (releasing()) {
      <app-release-dialog
        [worker]="worker()"
        (released)="released($event)"
        (dismissed)="releasing.set(false)"
        (reloaded)="reload()"
      />
    }
  `,
  styles: `
    :host {
      display: block;
    }

    .posting__place {
      font-weight: 650;
    }

    .posting__actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-2);
      margin-top: var(--space-4);
    }

    .posting__actions .btn {
      flex: 1 1 14rem;
    }

    .posting__note {
      margin-top: var(--space-3);
    }

    .posting__history {
      margin-top: var(--space-4);
      padding-top: var(--space-2);
      border-top: 1px solid var(--color-border);
    }

    .posting__history summary {
      min-height: var(--touch-target);
      display: flex;
      align-items: center;
      font-weight: 600;
      cursor: pointer;
    }

    .history {
      display: grid;
      gap: var(--space-3);
      margin: var(--space-2) 0 0;
      padding: 0;
      list-style: none;
    }

    .history li {
      display: grid;
      gap: 2px;
    }

    .history__what {
      font-weight: 600;
    }
  `,
})
export class WorkerPostingPanel {
  private readonly state = inject(WorkerDetailState);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);

  protected readonly worker = this.state.worker;
  protected readonly canManage = this.session.permit(Permission.workforceManage);

  protected readonly assigning = signal(false);
  protected readonly releasing = signal(false);

  protected readonly current = computed(() => this.worker().currentAssignment);
  protected readonly active = computed(() => this.worker().status === 'Active');
  protected readonly left = computed(() =>
    ['Resigned', 'Retired', 'Terminated'].includes(this.worker().status),
  );
  protected readonly place = computed(() => {
    const posting = this.current();
    return posting ? placeOf(posting) : '';
  });
  protected readonly since = computed(() => formatDate(this.current()?.startDate));

  protected readonly earlier = computed(() =>
    this.worker()
      .assignments.filter((item) => !item.isCurrent)
      .map((item) => ({
        id: item.id,
        place: placeOf(item),
        dates: item.endDate
          ? `${formatDate(item.startDate)} to ${formatDate(item.endDate)}`
          : `From ${formatDate(item.startDate)}`,
      })),
  );

  protected assigned(updated: WorkerDetail | null): void {
    this.assigning.set(false);
    if (updated) this.state.replace(updated);
    else void this.state.reload();
    this.toast.success('Worker assigned.');
  }

  protected released(updated: WorkerDetail): void {
    this.releasing.set(false);
    this.state.replace(updated);
    this.toast.success('Worker released from the project.');
  }

  protected reload(): void {
    this.assigning.set(false);
    this.releasing.set(false);
    void this.state.reload();
  }
}
