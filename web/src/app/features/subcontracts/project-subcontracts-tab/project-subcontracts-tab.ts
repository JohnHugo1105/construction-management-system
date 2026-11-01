import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { asApiError } from '../../../core/api/api-error';
import { Permission } from '../../../core/session/permissions';
import { SessionState } from '../../../core/session/session-state';
import { ApiErrorAlert } from '../../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../../shared/ui/button/button';
import { EmptyState } from '../../../shared/ui/empty-state/empty-state';
import { Icon } from '../../../shared/ui/icon/icon';
import { Skeleton } from '../../../shared/ui/skeleton/skeleton';
import { ProjectContext } from '../../projects/project-context';
import { SubcontractRows } from '../subcontract-rows';
import { SubcontractsApi } from '../subcontracts-api';

const TAB_PAGE_SIZE = 100;

/**
 * A project's subcontracts: who is doing which work, how much is certified and what is owed. "New subcontract" opens the form with
 * this project chosen, for people who draft subcontracts on a project that is not archived. It is a secondary button: the project
 * page above already has its own primary action.
 */
@Component({
  selector: 'app-project-subcontracts-tab',
  imports: [RouterLink, Button, Icon, EmptyState, Skeleton, ApiErrorAlert, SubcontractRows],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="stack">
      @if (canAdd() && items().length > 0) {
        <div class="toolbar">
          <a appButton routerLink="/subcontracts/new" [queryParams]="{ projectId: context.projectId() }">
            <app-icon name="plus" [size]="18" />
            New subcontract
          </a>
        </div>
      }
      @if (context.isArchived()) {
        <p class="text-small text-muted">This project is archived, so no subcontracts can be added or changed.</p>
      }

      @if (error(); as problem) {
        <app-api-error-alert [error]="problem" [allowRetry]="true" reloadLabel="Try again" (reload)="list.reload()" />
      } @else if (list.isLoading() && !list.hasValue()) {
        <app-skeleton variant="card" [lines]="2" label="Loading subcontracts" />
      } @else if (items().length > 0) {
        <app-subcontract-rows [items]="items()" [showProject]="false" />
        @if (total() > items().length) {
          <p class="text-small text-muted">
            Showing the newest {{ items().length }} of {{ total() }}.
            <a routerLink="/subcontracts" [queryParams]="{ project: context.projectId() }">See them all</a>
          </p>
        }
      } @else {
        <app-empty-state
          icon="contract"
          title="No subcontracts on this project"
          [message]="
            canAdd()
              ? 'Give work to another firm here: its sum and retention, then certify its claims as the work is done.'
              : 'Subcontracts given on this project will show here.'
          "
        >
          @if (canAdd()) {
            <a appButton routerLink="/subcontracts/new" [queryParams]="{ projectId: context.projectId() }">
              <app-icon name="plus" [size]="18" />
              New subcontract
            </a>
          }
        </app-empty-state>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .toolbar {
      display: flex;
      justify-content: flex-end;
    }

    @media (max-width: 47.99rem) {
      .toolbar .btn {
        flex: 1 1 auto;
      }
    }
  `,
})
export class ProjectSubcontractsTab {
  protected readonly context = inject(ProjectContext);
  private readonly api = inject(SubcontractsApi);
  private readonly session = inject(SessionState);

  protected readonly canAdd = computed(
    () => this.session.can(Permission.subcontractsManage) && !this.context.isArchived(),
  );

  protected readonly list = rxResource({
    params: () => this.context.projectId() ?? undefined,
    stream: ({ params }) =>
      this.api.list({ projectId: params, status: '', search: '', page: 1, pageSize: TAB_PAGE_SIZE }),
  });

  protected readonly items = computed(() => (this.list.hasValue() ? this.list.value().items : []));
  protected readonly total = computed(() => (this.list.hasValue() ? this.list.value().total : 0));
  protected readonly error = computed(() => {
    const failure = this.list.error();
    return failure ? asApiError(failure) : null;
  });
}
