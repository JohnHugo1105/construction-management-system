import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { Control } from '../../shared/ui/form-field/control';
import { Icon } from '../../shared/ui/icon/icon';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Pager } from '../../shared/ui/pager/pager';
import { SearchBox } from '../../shared/ui/search-box/search-box';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { SubcontractListState } from './subcontract-list-state';
import { STATUS_FILTERS } from './subcontract-presentation';
import { SubcontractRows } from './subcontract-rows';
import { SubcontractsApi } from './subcontracts-api';
import { SubcontractStatus } from './subcontracts.models';

/**
 * Every subcontract of the company, newest first: filter by state, project or words, page through them, open one. Only people who
 * draft subcontracts are offered "New subcontract". Before any exists, the empty state says what a subcontract is for.
 */
@Component({
  selector: 'app-subcontracts-page',
  imports: [
    RouterLink,
    PageHeader,
    Button,
    Icon,
    Pager,
    EmptyState,
    Skeleton,
    ApiErrorAlert,
    SearchBox,
    Control,
    SubcontractRows,
  ],
  providers: [SubcontractListState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Subcontracts" />

    <div class="stack">
      @if (!empty()) {
        <div class="toolbar">
          <app-search-box
            class="toolbar__search"
            label="Search subcontracts"
            placeholder="Number, scope or firm"
            [value]="state.query.values().search"
            (searched)="state.query.set({ search: $event })"
          />
          @if (projects().length > 1) {
            <label class="toolbar__project">
              <span class="visually-hidden">Project</span>
              <select appControl #projectSelect (change)="setProject(projectSelect.value)">
                <option value="" [selected]="state.query.values().project === ''">All projects</option>
                @for (project of projects(); track project.id) {
                  <option [value]="project.id" [selected]="state.query.values().project === project.id">
                    {{ project.name }}
                  </option>
                }
              </select>
            </label>
          }
          @if (canManage()) {
            <a appButton variant="primary" routerLink="/subcontracts/new">
              <app-icon name="plus" [size]="18" />
              New subcontract
            </a>
          }
        </div>

        <div class="filter-chips" role="group" aria-label="Filter by status">
          @for (filter of filters; track filter.value) {
            <button
              type="button"
              class="filter-chip"
              [attr.aria-pressed]="state.query.values().status === filter.value"
              (click)="setStatus(filter.value)"
            >
              {{ filter.label }}
            </button>
          }
        </div>
      }

      @if (state.error(); as problem) {
        <app-api-error-alert [error]="problem" [allowRetry]="true" reloadLabel="Try again" (reload)="state.reload()" />
      }

      @if (state.loadingFirst()) {
        <app-skeleton variant="card" [lines]="3" label="Loading subcontracts" />
      } @else if (state.items().length > 0) {
        <div [class.refreshing]="state.refreshing()" [attr.aria-busy]="state.refreshing()">
          <app-subcontract-rows [items]="state.items()" />
        </div>
        <app-pager
          [page]="state.query.values().page"
          [pageSize]="state.pageSize"
          [total]="state.total()"
          noun="subcontracts"
          (pageChange)="state.query.setPage($event)"
        />
      } @else if (!state.error()) {
        @if (state.query.isFiltered()) {
          <app-empty-state icon="search" title="No subcontracts match" message="Try another state, project or search.">
            <button appButton type="button" (click)="state.query.reset()">Show all subcontracts</button>
          </app-empty-state>
        } @else {
          <app-empty-state
            icon="contract"
            title="No subcontracts yet"
            [message]="
              canManage()
                ? 'A subcontract records work you give to another firm: its sum, retention, the claims you certify and what you pay.'
                : 'A project manager or cost engineer drafts subcontracts; they appear here once drafted.'
            "
          >
            @if (canManage()) {
              <a appButton variant="primary" routerLink="/subcontracts/new">
                <app-icon name="plus" [size]="18" />
                New subcontract
              </a>
            }
          </app-empty-state>
        }
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .toolbar {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-3);
      align-items: center;
    }

    .toolbar__search {
      flex: 1 1 14rem;
    }

    .toolbar__project {
      flex: 1 1 12rem;
      max-width: 20rem;
    }

    .refreshing {
      opacity: 0.6;
    }

    @media (max-width: 47.99rem) {
      .toolbar .btn {
        flex: 1 1 100%;
      }

      .toolbar__project {
        max-width: none;
      }
    }
  `,
})
export class SubcontractsPage {
  protected readonly state = inject(SubcontractListState);
  private readonly api = inject(SubcontractsApi);
  private readonly session = inject(SessionState);

  protected readonly canManage = this.session.permit(Permission.subcontractsManage);
  protected readonly filters = STATUS_FILTERS;

  private readonly projectList = rxResource({ stream: () => this.api.projects() });
  protected readonly projects = computed(() => (this.projectList.hasValue() ? this.projectList.value() : []));

  /** Nothing exists and nothing is filtered: the empty state carries the one next step, so the toolbar would only repeat it. */
  protected readonly empty = computed(
    () =>
      !this.state.loadingFirst() &&
      !this.state.error() &&
      this.state.items().length === 0 &&
      !this.state.query.isFiltered(),
  );

  protected setStatus(value: '' | SubcontractStatus): void {
    this.state.query.set({ status: value });
  }

  protected setProject(value: string): void {
    this.state.query.set({ project: value });
  }
}
