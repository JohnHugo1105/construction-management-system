import { Injectable, Signal, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { ProjectsApi } from './projects-api';
import { Project, SubProject } from './projects.models';

export type ProjectLoadStatus = 'loading' | 'ready' | 'error';

/**
 * The project a page under `/projects/:id` belongs to. The project detail page provides one and loads it; the tab components
 * (Budget, Costs, Team, Stock) inject it to learn the project's id, name, state and sub-projects, and to switch off writing when
 * the project is archived:
 *
 *     private readonly context = inject(ProjectContext);
 *     protected readonly canEdit = computed(() => this.session.can('costs.write') && !this.context.isArchived());
 *
 * A tab is only created once the project has loaded, so `project()` is always defined inside a tab. After a change that
 * affects the project itself, call `replace(dto)` with the API's answer or `reload()`.
 */
@Injectable()
export class ProjectContext {
  private readonly api = inject(ProjectsApi);
  private readonly session = inject(SessionState);
  private readonly current = signal<Project | null>(null);
  private readonly requestedId = signal<string | null>(null);

  /** Whether the project is loading, shown, or failed to load. */
  readonly status = signal<ProjectLoadStatus>('loading');
  readonly error = signal<ApiError | null>(null);

  /** True while a reload runs and the project already on screen stays visible. */
  readonly refreshing = signal(false);

  /** The loaded project. Read it only once `status()` is 'ready' (tabs always are). */
  readonly project: Signal<Project> = computed(() => {
    const project = this.current();
    if (!project)
      throw new Error(
        'The project has not loaded yet. Read ProjectContext.project() only inside a loaded project.',
      );
    return project;
  });

  readonly projectId = computed(() => this.current()?.id ?? this.requestedId());
  readonly name = computed(() => this.current()?.name ?? '');
  readonly isArchived = computed(() => this.current()?.status === 'Archived');
  readonly subProjects = computed<readonly SubProject[]>(() => this.current()?.subProjects ?? []);

  /** Sub-projects that can still receive new records. */
  readonly activeSubProjects = computed(() =>
    this.subProjects().filter((sub) => sub.status === 'Active'),
  );

  /** The person may change the project itself, and it is not archived. */
  readonly canManage = computed(
    () => this.session.can(Permission.projectsManage) && !this.isArchived(),
  );

  /** Loads a project, or switches to another one. */
  async load(id: string): Promise<void> {
    if (this.requestedId() !== id) {
      this.requestedId.set(id);
      this.current.set(null);
      this.status.set('loading');
      this.error.set(null);
    }
    await this.fetch(id);
  }

  /** Fetches the project again, keeping the current one on screen meanwhile. */
  async reload(): Promise<void> {
    const id = this.requestedId();
    if (id) await this.fetch(id);
  }

  /** Takes the project the API returned from a change (every project action answers with the whole project). */
  replace(project: Project): void {
    this.current.set(project);
    this.status.set('ready');
    this.error.set(null);
  }

  private async fetch(id: string): Promise<void> {
    this.refreshing.set(this.current() !== null);
    try {
      const project = await firstValueFrom(this.api.get(id));
      if (this.requestedId() === id) this.replace(project);
    } catch (failure) {
      if (this.requestedId() === id && this.current() === null) {
        this.error.set(asApiError(failure));
        this.status.set('error');
      }
    } finally {
      this.refreshing.set(false);
    }
  }
}
