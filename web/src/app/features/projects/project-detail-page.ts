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
import { RouterLink, RouterOutlet } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Menu } from '../../shared/ui/menu/menu';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { TabLink, TabNav } from '../../shared/ui/tab-nav/tab-nav';
import { ArchiveProjectDialog } from './archive-project-dialog';
import { ProjectContext } from './project-context';
import {
  LIFECYCLE_DONE,
  LIFECYCLE_LABELS,
  LifecycleAction,
  PROJECT_STATUS,
  lifecyclePlan,
} from './project-status';
import { ProjectsApi } from './projects-api';
import { Project } from './projects.models';

/**
 * A project: its name and state, the one next step (with the rest under "More"), and tabs for what belongs to it. The page
 * provides the ProjectContext that the tabs under it read. An archived project says so and is read-only.
 */
@Component({
  selector: 'app-project-detail-page',
  imports: [
    RouterLink,
    RouterOutlet,
    PageHeader,
    Alert,
    ApiErrorAlert,
    Button,
    Menu,
    Skeleton,
    StatusChip,
    TabNav,
    ArchiveProjectDialog,
  ],
  providers: [ProjectContext],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './project-detail-page.html',
  styleUrl: './project-detail-page.scss',
})
export class ProjectDetailPage {
  /** Bound from the route (`/projects/:id`). */
  readonly id = input.required<string>();

  protected readonly context = inject(ProjectContext);
  private readonly session = inject(SessionState);
  private readonly api = inject(ProjectsApi);
  private readonly toast = inject(ToastService);

  protected readonly labels = LIFECYCLE_LABELS;
  protected readonly archiveOpen = signal(false);
  protected readonly busyAction = signal<LifecycleAction | null>(null);
  protected readonly actionError = signal<ApiError | null>(null);

  protected readonly canManage = this.session.permit(Permission.projectsManage);

  protected readonly summary = computed(() => {
    if (this.context.status() !== 'ready') return null;
    const project = this.context.project();
    return {
      status: PROJECT_STATUS[project.status],
      about: [project.client, project.location].filter(Boolean).join(' · '),
      plan: lifecyclePlan(project.status),
      archiveNote: this.archiveSentence(project),
    };
  });

  protected readonly tabs = computed<TabLink[]>(() => {
    const base = ['/projects', this.id()];
    const tabs: TabLink[] = [{ label: 'Overview', link: base, exact: true }];
    // Money first, in the order a project manager asks the questions: what we planned, how far along, what changed, what we have
    // spent, what is promised, who we owe, what we bill. Each tab is hidden from anyone who may not read it.
    if (this.session.can(Permission.costsRead))
      tabs.push({ label: 'Budget', link: [...base, 'budget'] });
    if (this.session.can(Permission.progressRead))
      tabs.push({ label: 'Progress', link: [...base, 'progress'] });
    if (this.session.can(Permission.changeOrdersRead))
      tabs.push({ label: 'Changes', link: [...base, 'changes'] });
    if (this.session.can(Permission.costsRead))
      tabs.push({ label: 'Costs', link: [...base, 'costs'] });
    if (this.session.can(Permission.purchasingRead))
      tabs.push({ label: 'Purchases', link: [...base, 'purchases'] });
    if (this.session.can(Permission.subcontractsRead))
      tabs.push({ label: 'Subcontracts', link: [...base, 'subcontracts'] });
    if (this.session.can(Permission.billingRead))
      tabs.push({ label: 'Billing', link: [...base, 'billing'] });
    if (this.session.can(Permission.workforceRead))
      tabs.push({ label: 'Team', link: [...base, 'team'] });
    if (this.session.can(Permission.inventoryRead))
      tabs.push({ label: 'Stock', link: [...base, 'stock'] });
    if (this.session.can(Permission.diaryRead))
      tabs.push({ label: 'Diary', link: [...base, 'diary'] });
    if (this.session.can(Permission.documentsRead))
      tabs.push({ label: 'Documents', link: [...base, 'documents'] });
    return tabs;
  });

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => void this.context.load(id));
    });
  }

  protected async perform(action: LifecycleAction): Promise<void> {
    if (this.busyAction() !== null) return;
    if (action === 'archive') {
      this.archiveOpen.set(true);
      return;
    }

    const project = this.context.project();
    this.busyAction.set(action);
    this.actionError.set(null);
    try {
      const updated = await firstValueFrom(
        this.api.transition(project.id, action, project.version),
      );
      this.context.replace(updated);
      this.toast.success(LIFECYCLE_DONE[action]);
    } catch (failure) {
      this.actionError.set(asApiError(failure));
    } finally {
      this.busyAction.set(null);
    }
  }

  protected archived(project: Project): void {
    this.context.replace(project);
    this.archiveOpen.set(false);
    this.toast.success(LIFECYCLE_DONE.archive);
  }

  protected async reload(): Promise<void> {
    this.actionError.set(null);
    this.archiveOpen.set(false);
    await this.context.reload();
  }

  private archiveSentence(project: Project): string {
    if (project.status !== 'Archived') return '';
    if (project.archiveReason === 'Finished') return ' (finished project)';
    return project.archiveNote ? ` (${project.archiveNote})` : '';
  }
}
