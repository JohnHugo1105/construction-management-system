import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { formatDate } from '../../shared/presentation/business-time';
import { formatMoney } from '../../shared/presentation/money';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { Icon } from '../../shared/ui/icon/icon';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Pager } from '../../shared/ui/pager/pager';
import { SearchBox } from '../../shared/ui/search-box/search-box';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { ProjectListState } from './project-list-state';
import { PROJECT_FILTERS, PROJECT_STATUS, filterLabel } from './project-status';

/** The project list: search, status chips, a table on wide screens and cards on phones, paged by the server. */
@Component({
  selector: 'app-project-list-page',
  imports: [
    RouterLink,
    PageHeader,
    SearchBox,
    Button,
    Icon,
    StatusChip,
    Pager,
    EmptyState,
    Skeleton,
    ApiErrorAlert,
  ],
  providers: [ProjectListState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './project-list-page.html',
  styleUrl: './project-list-page.scss',
})
export class ProjectListPage {
  protected readonly state = inject(ProjectListState);
  protected readonly viewport = inject(Viewport);
  private readonly session = inject(SessionState);

  protected readonly canManage = this.session.permit(Permission.projectsManage);
  /** The contract price is what the company will be paid: the API sends it only to people who see costs, so the column is theirs too. */
  protected readonly canSeePrice = this.session.permit(Permission.costsRead);
  protected readonly filters = PROJECT_FILTERS;
  protected readonly filterLabel = filterLabel;

  /** The rows with their words and amounts worked out, so the template only prints them. */
  protected readonly rows = computed(() => {
    const currency = this.session.currencyCode();
    return this.state.items().map((project) => ({
      id: project.id,
      name: project.name,
      subtitle: [project.client, project.location].filter(Boolean).join(' · '),
      status: PROJECT_STATUS[project.status],
      price:
        project.contractPriceMinor === null
          ? 'Not set'
          : formatMoney(project.contractPriceMinor, currency),
      dates: project.targetEndDate
        ? `${formatDate(project.startDate)} to ${formatDate(project.targetEndDate)}`
        : `From ${formatDate(project.startDate)}`,
      subProjectCount: project.subProjectCount,
      subProjectLabel:
        project.subProjectCount === 1 ? '1 sub-project' : `${project.subProjectCount} sub-projects`,
    }));
  });
}
