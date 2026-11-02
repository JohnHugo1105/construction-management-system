import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { ToastService } from '../../core/notifications/toast-service';
import { SessionState } from '../../core/session/session-state';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { Control } from '../../shared/ui/form-field/control';
import { Icon } from '../../shared/ui/icon/icon';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Pager } from '../../shared/ui/pager/pager';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { CollaborationApi } from './collaboration-api';
import { STATUS_CHOICES, taskLook } from './collaboration-presentation';
import { TaskScope, TaskStatus, WorkTask } from './collaboration.models';
import { TaskFormDialog } from './task-form-dialog';
import { TaskListState } from './task-list-state';

interface ScopeChoice {
  readonly value: TaskScope;
  readonly label: string;
}

/**
 * Tasks: the work given to you (the default) or by you, Open first with the overdue and soonest due on top. "Add task" is the one primary
 * action. The state and the project are folded under "Filters" until wanted; an Administrator can also see every task. Tapping a task
 * opens it with its discussion. A task never shows an amount: it points at the record that does.
 */
@Component({
  selector: 'app-tasks-page',
  imports: [
    RouterLink,
    PageHeader,
    Button,
    Icon,
    Pager,
    EmptyState,
    Skeleton,
    StatusChip,
    ApiErrorAlert,
    Control,
    TaskFormDialog,
  ],
  providers: [TaskListState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Tasks">
      <button appButton variant="primary" type="button" (click)="adding.set(true)">
        <app-icon name="plus" [size]="18" />
        Add task
      </button>
    </app-page-header>

    <div class="stack">
      <div class="toolbar">
        <div class="filter-chips" role="group" aria-label="Whose tasks">
          @for (choice of scopes(); track choice.value) {
            <button
              type="button"
              class="filter-chip"
              [attr.aria-pressed]="state.query.values().scope === choice.value"
              (click)="state.query.set({ scope: choice.value })"
            >
              {{ choice.label }}
            </button>
          }
        </div>
        <button
          appButton
          type="button"
          [attr.aria-expanded]="filtersOpen()"
          aria-controls="task-filters"
          (click)="toggleFilters()"
        >
          <app-icon name="sliders" [size]="18" />
          Filters
        </button>
      </div>

      @if (filtersOpen()) {
        <div id="task-filters" class="filters">
          <label>
            <span class="filters__label">Show</span>
            <select
              appControl
              #statusSelect
              (change)="state.query.set({ status: asStatus(statusSelect.value) })"
            >
              @for (choice of statuses; track choice.value) {
                <option
                  [value]="choice.value"
                  [selected]="state.query.values().status === choice.value"
                >
                  {{ choice.label }}
                </option>
              }
            </select>
          </label>
          @if (projects().length > 0) {
            <label>
              <span class="filters__label">Project</span>
              <select
                appControl
                #projectSelect
                (change)="state.query.set({ project: projectSelect.value })"
              >
                <option value="" [selected]="state.query.values().project === ''">
                  All projects
                </option>
                @for (project of projects(); track project.id) {
                  <option
                    [value]="project.id"
                    [selected]="state.query.values().project === project.id"
                  >
                    {{ project.name }}
                  </option>
                }
              </select>
            </label>
          }
        </div>
      }

      @if (state.error(); as problem) {
        <app-api-error-alert
          [error]="problem"
          [allowRetry]="true"
          reloadLabel="Try again"
          (reload)="state.reload()"
        />
      }

      @if (state.loadingFirst()) {
        <app-skeleton variant="card" [lines]="3" label="Loading tasks" />
      } @else if (rows().length > 0) {
        <ul
          class="card-list"
          [class.refreshing]="state.refreshing()"
          [attr.aria-busy]="state.refreshing()"
        >
          @for (row of rows(); track row.id) {
            <li class="row-card">
              <div class="row-card__head">
                <a
                  class="row-card__title row-link row-link--stretch"
                  [routerLink]="['/tasks', row.id]"
                  >{{ row.title }}</a
                >
                <app-status-chip
                  [label]="row.look.label"
                  [tone]="row.look.tone"
                  [icon]="row.look.icon"
                />
              </div>
              <p class="row-card__meta">
                @if (row.due) {
                  <span>{{ row.due }}</span>
                }
                @if (row.who) {
                  <span>{{ row.who }}</span>
                }
                @if (row.project) {
                  <span>{{ row.project }}</span>
                }
              </p>
            </li>
          }
        </ul>
        <app-pager
          [page]="state.query.values().page"
          [pageSize]="state.pageSize"
          [total]="state.total()"
          noun="tasks"
          (pageChange)="state.query.setPage($event)"
        />
      } @else if (!state.error()) {
        <app-empty-state icon="check" [title]="emptyWords().title" [message]="emptyWords().message">
          @if (state.query.isFiltered() && !onlyScopeChanged()) {
            <button appButton type="button" (click)="showOpen()">Show open tasks</button>
          }
        </app-empty-state>
      }
    </div>

    @if (adding()) {
      <app-task-form-dialog
        [projectId]="state.query.values().project || null"
        (saved)="added($event)"
        (dismissed)="adding.set(false)"
      />
    }
  `,
  styles: `
    :host {
      display: block;
    }

    .toolbar {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-2) var(--space-3);
      align-items: center;
      justify-content: space-between;
    }

    .toolbar .filter-chips {
      flex: 1 1 auto;
      min-width: 0;
    }

    .filters {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(11rem, 1fr));
      gap: var(--space-3);
      padding: var(--space-3);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      background: var(--color-surface);
    }

    .filters__label {
      display: block;
      margin-bottom: var(--space-1);
      font-size: var(--text-sm);
      color: var(--color-text-muted);
    }

    .row-card__title {
      overflow-wrap: anywhere;
    }

    .refreshing {
      opacity: 0.6;
    }

    @media (max-width: 47.99rem) {
      app-page-header .btn--primary {
        flex: 1 1 100%;
      }
    }
  `,
})
export class TasksPage {
  protected readonly state = inject(TaskListState);
  private readonly api = inject(CollaborationApi);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  protected readonly statuses = STATUS_CHOICES;
  protected readonly adding = signal(false);
  private readonly opened = signal(false);

  private readonly isAdministrator = computed(() => this.session.roles().includes('Administrator'));

  protected readonly scopes = computed<readonly ScopeChoice[]>(() => [
    { value: 'assigned', label: 'Assigned to me' },
    { value: 'created', label: 'Created by me' },
    ...(this.isAdministrator() ? [{ value: 'all' as const, label: 'Every task' }] : []),
  ]);

  private readonly projectList = rxResource({ stream: () => this.api.projects() });
  protected readonly projects = computed(() =>
    this.projectList.hasValue() ? this.projectList.value() : [],
  );

  /** The rows as shown: the due words the server sent, and the other person (who it is for, or who gave it). */
  protected readonly rows = computed(() => {
    const scope = this.state.query.values().scope;
    const me = this.session.session()?.userId;
    return this.state.items().map((item) => ({
      id: item.id,
      title: item.title,
      look: taskLook(item),
      due: item.dueDetail ?? '',
      who:
        scope === 'assigned'
          ? item.createdByUserId === me
            ? 'From you'
            : `From ${item.createdByName ?? 'a colleague'}`
          : item.assigneeUserId === me
            ? 'For you'
            : `For ${item.assigneeName ?? 'a colleague'}`,
      project: item.projectName ?? '',
    }));
  });

  /** The filters stay folded until asked for, but open by themselves when the page was reached with one set. */
  protected readonly filtersOpen = computed(
    () =>
      this.opened() ||
      this.state.query.values().status !== 'Open' ||
      this.state.query.values().project !== '',
  );

  /** Only the scope differs from the default: the list is simply empty, not filtered away. */
  protected readonly onlyScopeChanged = computed(
    () => this.state.query.values().status === 'Open' && this.state.query.values().project === '',
  );

  protected readonly emptyWords = computed(() => {
    const values = this.state.query.values();
    if (!this.onlyScopeChanged())
      return { title: 'No tasks match', message: 'Try another state or project.' };
    switch (values.scope) {
      case 'created':
        return {
          title: 'No open tasks from you',
          message: 'Tasks you give to others appear here until they are done.',
        };
      case 'all':
        return { title: 'No open tasks', message: 'Nobody in the company has an open task.' };
      default:
        return {
          title: 'Nothing assigned to you',
          message: 'When someone gives you a task, it appears here and you are told in the app.',
        };
    }
  });

  protected toggleFilters(): void {
    this.opened.set(!this.filtersOpen());
  }

  protected showOpen(): void {
    this.state.query.set({ status: 'Open', project: '' });
  }

  protected added(task: WorkTask): void {
    this.adding.set(false);
    this.toast.success(
      task.assigneeUserId === this.session.session()?.userId
        ? 'Task saved.'
        : `Task saved. ${task.assigneeName ?? 'They'} will see it in the app.`,
    );
    void this.router.navigate(['/tasks', task.id]);
  }

  protected asStatus(value: string): '' | TaskStatus {
    return STATUS_CHOICES.find((choice) => choice.value === value)?.value ?? 'Open';
  }
}
