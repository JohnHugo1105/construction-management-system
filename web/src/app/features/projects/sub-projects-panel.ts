import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Observable, firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { formatDate } from '../../shared/presentation/business-time';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { ConfirmDialog } from '../../shared/ui/dialog/confirm-dialog';
import { Icon } from '../../shared/ui/icon/icon';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { ProjectContext } from './project-context';
import { ProjectsApi } from './projects-api';
import { Project, SubProject } from './projects.models';
import { SubProjectDialog } from './sub-project-dialog';

type DialogState =
  { readonly kind: 'add' } | { readonly kind: 'edit'; readonly subProject: SubProject };

/**
 * The sub-projects of the open project: add, edit, archive and restore. The list shows only what exists, with no empty
 * counters; managers get the buttons, and an archived project is read-only for everyone.
 */
@Component({
  selector: 'app-sub-projects-panel',
  imports: [Button, Icon, StatusChip, ApiErrorAlert, ConfirmDialog, SubProjectDialog],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './sub-projects-panel.html',
  styleUrl: './sub-projects-panel.scss',
})
export class SubProjectsPanel {
  protected readonly context = inject(ProjectContext);
  private readonly api = inject(ProjectsApi);
  private readonly toast = inject(ToastService);

  protected readonly dialog = signal<DialogState | null>(null);
  protected readonly archiveTarget = signal<SubProject | null>(null);
  protected readonly working = signal(false);
  protected readonly error = signal<ApiError | null>(null);

  protected readonly rows = computed(() =>
    this.context.subProjects().map((sub) => ({
      sub,
      started: `Started ${formatDate(sub.startDate)}`,
      isArchived: sub.status === 'Archived',
    })),
  );

  protected editing(): SubProject | null {
    const state = this.dialog();
    return state?.kind === 'edit' ? state.subProject : null;
  }

  protected reload(): void {
    this.error.set(null);
    void this.context.reload();
  }

  protected saved(): void {
    this.toast.success(this.editing() ? 'Sub-project saved.' : 'Sub-project added.');
    this.dialog.set(null);
  }

  protected async archive(): Promise<void> {
    const target = this.archiveTarget();
    if (!target) return;
    const done = await this.run((project) =>
      this.api.archiveSubProject(project.id, target.id, project.version),
    );
    if (done) {
      this.toast.success('Sub-project archived.');
      this.archiveTarget.set(null);
    }
  }

  protected async restore(sub: SubProject): Promise<void> {
    if (
      await this.run((project) => this.api.restoreSubProject(project.id, sub.id, project.version))
    ) {
      this.toast.success('Sub-project restored.');
    }
  }

  /** Runs one change against the open project and keeps the answer. Returns whether it worked. */
  private async run(change: (project: Project) => Observable<Project>): Promise<boolean> {
    if (this.working()) return false;
    this.working.set(true);
    this.error.set(null);
    try {
      this.context.replace(await firstValueFrom(change(this.context.project())));
      return true;
    } catch (failure) {
      this.error.set(asApiError(failure));
      this.archiveTarget.set(null);
      return false;
    } finally {
      this.working.set(false);
    }
  }
}
