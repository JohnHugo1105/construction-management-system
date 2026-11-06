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
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { SessionState } from '../../core/session/session-state';
import { formatDateTime } from '../../shared/presentation/business-time';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { CollaborationApi } from './collaboration-api';
import { recordLink, taskLook } from './collaboration-presentation';
import { WorkTask } from './collaboration.models';
import { Discussion } from './discussion';
import { TaskState } from './task-state';
import { TaskStep, TaskStepDialog } from './task-step-dialog';

/**
 * One task: what to do, for whom and by when, the project and the record it is about, and its discussion. The one next step leads (Mark
 * as done while it is open, Reopen task once it is not); reassigning and cancelling sit beside it for the person who created it and
 * Administrators. On an archived project the task is read-only and says so. A task never shows an amount.
 */
@Component({
  selector: 'app-task-page',
  imports: [
    RouterLink,
    PageHeader,
    ApiErrorAlert,
    Button,
    Skeleton,
    StatusChip,
    Discussion,
    TaskStepDialog,
  ],
  providers: [TaskState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header [title]="title()" backLabel="All tasks" [backLink]="['/tasks']" />

    @switch (state.status()) {
      @case ('loading') {
        <app-skeleton [lines]="4" label="Loading task" />
      }
      @case ('error') {
        <div class="stack">
          <app-api-error-alert
            [error]="state.error()"
            [allowRetry]="!state.error()?.isNotFound"
            reloadLabel="Try again"
            (reload)="reload()"
          />
          <p><a appButton routerLink="/tasks">Back to tasks</a></p>
        </div>
      }
      @case ('ready') {
        @if (view(); as info) {
          <div class="stack stack--loose" [attr.aria-busy]="state.refreshing()">
            <div class="stack">
              <div class="head">
                <app-status-chip
                  [label]="info.look.label"
                  [tone]="info.look.tone"
                  [icon]="info.look.icon"
                />
                @if (info.due) {
                  <span class="head__due">{{ info.due }}</span>
                }
              </div>
              @if (info.archivedNote) {
                <p class="text-small text-muted">{{ info.archivedNote }}</p>
              }
              @if (stepError(); as problem) {
                <app-api-error-alert
                  [error]="problem"
                  [allowRetry]="problem.status === 409"
                  reloadLabel="Reload task"
                  (reload)="reload()"
                />
              }
              @if (
                info.actions.complete ||
                info.actions.reopen ||
                info.actions.reassign ||
                info.actions.cancel
              ) {
                <div class="actions">
                  @if (info.actions.complete) {
                    <button
                      appButton
                      variant="primary"
                      type="button"
                      [busy]="busy()"
                      (click)="complete()"
                    >
                      Mark as done
                    </button>
                  }
                  @if (info.actions.reopen) {
                    <button
                      appButton
                      variant="primary"
                      type="button"
                      [busy]="busy()"
                      (click)="reopen()"
                    >
                      Reopen task
                    </button>
                  }
                  @if (info.actions.reassign) {
                    <button appButton type="button" (click)="open.set('reassign')">Reassign</button>
                  }
                  @if (info.actions.cancel) {
                    <button appButton variant="ghost" type="button" (click)="open.set('cancel')">
                      Cancel task
                    </button>
                  }
                </div>
              }
              <dl class="def-list">
                <div>
                  <dt>For</dt>
                  <dd>{{ info.assignee }}</dd>
                </div>
                <div>
                  <dt>From</dt>
                  <dd>{{ info.creator }}</dd>
                </div>
                @if (info.project; as project) {
                  <div>
                    <dt>Project</dt>
                    <dd>
                      <a [routerLink]="['/projects', project.id]">{{ project.name }}</a>
                    </dd>
                  </div>
                }
                @if (info.about; as about) {
                  <div>
                    <dt>About</dt>
                    <dd>
                      <a [routerLink]="about.link" [queryParams]="about.queryParams">{{
                        about.label
                      }}</a>
                    </dd>
                  </div>
                }
              </dl>
              @if (info.description) {
                <p class="description">{{ info.description }}</p>
              }
              @if (info.closing) {
                <p class="text-small text-muted">{{ info.closing }}</p>
              }
            </div>

            <app-discussion kind="Task" [targetId]="info.id" />
          </div>
        }
      }
    }

    @if (state.status() === 'ready') {
      @if (open(); as step) {
        <app-task-step-dialog
          [step]="step"
          [task]="state.task()"
          (done)="stepDone($event, step)"
          (dismissed)="open.set(null)"
          (reloaded)="open.set(null); reload()"
        />
      }
    }
  `,
  styles: `
    :host {
      display: block;
    }

    .head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--space-2) var(--space-3);
    }

    .head__due {
      color: var(--color-text-muted);
    }

    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-2);
    }

    @media (max-width: 47.99rem) {
      .actions .btn--primary {
        flex: 1 1 100%;
      }
    }

    .def-list {
      margin: 0;

      dt {
        font-size: var(--text-sm);
        color: var(--color-text-muted);
      }

      dd {
        margin: 0;
        overflow-wrap: anywhere;
      }

      a {
        display: inline-block;
        padding: 10px 0;
        margin: -10px 0;
        color: var(--color-brand-text);
        font-weight: 600;
      }
    }

    .description {
      white-space: pre-line;
      overflow-wrap: anywhere;
    }
  `,
})
export class TaskPage {
  /** Bound from the route (`/tasks/:id`). */
  readonly id = input.required<string>();

  protected readonly state = inject(TaskState);
  private readonly api = inject(CollaborationApi);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);

  protected readonly open = signal<TaskStep | null>(null);
  protected readonly busy = signal(false);
  protected readonly stepError = signal<ApiError | null>(null);

  private readonly ready = computed(() =>
    this.state.status() === 'ready' ? this.state.task() : null,
  );

  protected readonly title = computed(() => this.ready()?.title ?? 'Task');

  protected readonly view = computed(() => {
    const task = this.ready();
    if (!task) return null;
    const me = this.session.session()?.userId;
    const about =
      task.linkKind && task.linkId && task.linkLabel && task.linkKind !== 'Project'
        ? {
            ...recordLink(task.linkKind, task.linkId, task.projectId),
            label: capitalize(task.linkLabel),
          }
        : null;
    return {
      id: task.id,
      look: taskLook(task),
      due: task.dueDetail ?? '',
      archivedNote: task.projectArchived
        ? 'This project is archived, so this task can be read but not changed. Restore the project to change it.'
        : '',
      actions: task.actions,
      assignee: task.assigneeUserId === me ? 'You' : (task.assigneeName ?? 'A colleague'),
      creator: task.createdByUserId === me ? 'You' : (task.createdByName ?? 'A colleague'),
      project:
        task.projectId && task.projectName ? { id: task.projectId, name: task.projectName } : null,
      about,
      description: task.description ?? '',
      closing: closing(task, this.session.timeZoneId()),
    };
  });

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => void this.state.load(id));
    });
  }

  protected reload(): void {
    this.stepError.set(null);
    void this.state.reload();
  }

  protected complete(): Promise<void> {
    const task = this.state.task();
    return this.run(this.api.completeTask(task.id, task.version), 'Task marked as done.');
  }

  protected reopen(): Promise<void> {
    const task = this.state.task();
    return this.run(this.api.reopenTask(task.id, task.version), 'Task reopened.');
  }

  protected stepDone(task: WorkTask, step: TaskStep): void {
    this.open.set(null);
    this.state.replace(task);
    this.toast.success(
      step === 'reassign'
        ? `Task given to ${task.assigneeName ?? 'your colleague'}. They are told in the app.`
        : 'Task cancelled.',
    );
  }

  private async run(
    call: ReturnType<CollaborationApi['completeTask']>,
    message: string,
  ): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.stepError.set(null);
    try {
      this.state.replace(await firstValueFrom(call));
      this.toast.success(message);
    } catch (failure) {
      this.stepError.set(asApiError(failure));
    } finally {
      this.busy.set(false);
    }
  }
}

/** "Done by Pia Santos, 2 Nov 2026, 3:10 pm." or "Cancelled by Ana Reyes, ...: wrong project." */
function closing(task: WorkTask, timeZone: string): string {
  if (task.status === 'Done' && task.completedAtUtc)
    return `Done by ${task.completedByName ?? 'a colleague'}, ${formatDateTime(task.completedAtUtc, timeZone)}.`;
  if (task.status === 'Cancelled' && task.cancelledAtUtc) {
    const reason = task.cancelReason ? ` Reason: ${task.cancelReason}` : '';
    return `Cancelled by ${task.cancelledByName ?? 'a colleague'}, ${formatDateTime(task.cancelledAtUtc, timeZone)}.${reason}`;
  }
  return '';
}

function capitalize(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1);
}
