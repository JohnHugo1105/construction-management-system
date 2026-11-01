import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { ToastService } from '../../core/notifications/toast-service';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { formatMoney } from '../../shared/presentation/money';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { Control } from '../../shared/ui/form-field/control';
import { Icon } from '../../shared/ui/icon/icon';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Pager } from '../../shared/ui/pager/pager';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { BondFormDialog } from './bond-form-dialog';
import { BondListState } from './bond-list-state';
import { BondRows } from './bond-rows';
import { BondsApi } from './bonds-api';
import { BondState, STATE_CHOICES } from './bonds-presentation';
import { BondDirection, BondKind, BondTotals } from './bonds.models';

const KIND_FILTERS: readonly { value: '' | BondKind; label: string }[] = [
  { value: '', label: 'Every kind' },
  { value: 'Performance', label: 'Performance' },
  { value: 'AdvancePayment', label: 'Advance payment' },
  { value: 'Warranty', label: 'Warranty' },
  { value: 'Bid', label: 'Bid' },
  { value: 'Other', label: 'Other' },
];

/**
 * The bonds register: the bonds in force on every project, the one that lapses soonest first. It opens on the Active bonds, says in one
 * line what is posted and held and, only when something needs it, how many have expired or expire within 30 days (each a shortcut to
 * those bonds). Filters are folded until wanted. Only people who manage bonds are offered "Add bond". A bond is a record to watch: nothing
 * here changes any cost, bill or payment.
 */
@Component({
  selector: 'app-bonds-page',
  imports: [
    PageHeader,
    Button,
    Icon,
    Pager,
    EmptyState,
    Skeleton,
    ApiErrorAlert,
    Control,
    BondRows,
    BondFormDialog,
  ],
  providers: [BondListState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Bonds" />

    <div class="stack">
      @if (!empty()) {
        <div class="toolbar">
          <button
            appButton
            type="button"
            [attr.aria-expanded]="filtersOpen()"
            aria-controls="bond-filters"
            (click)="toggleFilters()"
          >
            <app-icon name="sliders" [size]="18" />
            Filters
          </button>
          @if (canManage()) {
            <button appButton variant="primary" type="button" (click)="adding.set(true)">
              <app-icon name="plus" [size]="18" />
              Add bond
            </button>
          }
        </div>

        @if (filtersOpen()) {
          <div id="bond-filters" class="filters">
            <label>
              <span class="filters__label">Show</span>
              <select
                appControl
                #stateSelect
                (change)="state.query.set({ state: asState(stateSelect.value) })"
              >
                @for (choice of states; track choice.value) {
                  <option
                    [value]="choice.value"
                    [selected]="state.query.values().state === choice.value"
                  >
                    {{ choice.label }}
                  </option>
                }
              </select>
            </label>
            @if (projects().length > 1) {
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
            <label>
              <span class="filters__label">Whose</span>
              <select
                appControl
                #directionSelect
                (change)="state.query.set({ direction: asDirection(directionSelect.value) })"
              >
                <option value="" [selected]="state.query.values().direction === ''">
                  Posted and received
                </option>
                <option value="Posted" [selected]="state.query.values().direction === 'Posted'">
                  Posted by us
                </option>
                <option value="Received" [selected]="state.query.values().direction === 'Received'">
                  Received from firms
                </option>
              </select>
            </label>
            <label>
              <span class="filters__label">Kind</span>
              <select
                appControl
                #kindSelect
                (change)="state.query.set({ kind: asKind(kindSelect.value) })"
              >
                @for (kind of kinds; track kind.value) {
                  <option
                    [value]="kind.value"
                    [selected]="state.query.values().kind === kind.value"
                  >
                    {{ kind.label }}
                  </option>
                }
              </select>
            </label>
          </div>
        }

        @if (summary(); as figures) {
          <p class="summary">{{ figures }}</p>
        }
        @if (totals(); as t) {
          @if (t.expiredCount > 0 || t.expiringSoonCount > 0) {
            <p class="attention" role="group" aria-label="Bonds that need attention">
              <span>Needs attention:</span>
              @if (t.expiredCount > 0) {
                <button appButton type="button" (click)="state.query.set({ state: 'Expired' })">
                  {{ t.expiredCount }} {{ t.expiredCount === 1 ? 'has' : 'have' }} expired
                </button>
              }
              @if (t.expiringSoonCount > 0) {
                <button
                  appButton
                  type="button"
                  (click)="state.query.set({ state: 'ExpiringSoon' })"
                >
                  {{ t.expiringSoonCount }}
                  {{ t.expiringSoonCount === 1 ? 'expires' : 'expire' }} within 30 days
                </button>
              }
            </p>
          }
        }
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
        <app-skeleton variant="card" [lines]="3" label="Loading bonds" />
      } @else if (state.items().length > 0) {
        <div [class.refreshing]="state.refreshing()" [attr.aria-busy]="state.refreshing()">
          <app-bond-rows [items]="state.items()" />
        </div>
        <app-pager
          [page]="state.query.values().page"
          [pageSize]="state.pageSize"
          [total]="state.total()"
          noun="bonds"
          (pageChange)="state.query.setPage($event)"
        />
      } @else if (!state.error()) {
        @if (state.query.isFiltered()) {
          <app-empty-state
            icon="search"
            title="No bonds match"
            message="Try another state, project or kind."
          >
            <button appButton type="button" (click)="state.query.reset()">Show active bonds</button>
          </app-empty-state>
        } @else {
          <app-empty-state
            icon="shield"
            title="No active bonds"
            [message]="
              canManage()
                ? 'A bond is a guarantee a bank or surety gives, such as a performance bond. Record the ones you posted or received so none lapses unnoticed.'
                : 'Bonds your company posted or received appear here once someone records them.'
            "
          >
            @if (canManage()) {
              <button appButton variant="primary" type="button" (click)="adding.set(true)">
                <app-icon name="plus" [size]="18" />
                Add bond
              </button>
            }
            <button appButton type="button" (click)="state.query.set({ state: 'All' })">
              Show every bond
            </button>
          </app-empty-state>
        }
      }
    </div>

    @if (adding()) {
      <app-bond-form-dialog
        (saved)="added()"
        (dismissed)="adding.set(false)"
        (reloaded)="adding.set(false)"
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
      gap: var(--space-3);
      align-items: center;
      justify-content: space-between;
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

    .summary {
      margin: 0;
    }

    .attention {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--space-2) var(--space-3);
      margin: 0;
    }

    .refreshing {
      opacity: 0.6;
    }

    @media (max-width: 47.99rem) {
      .toolbar .btn--primary {
        flex: 1 1 auto;
      }
    }
  `,
})
export class BondsPage {
  protected readonly state = inject(BondListState);
  private readonly api = inject(BondsApi);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);

  protected readonly canManage = this.session.permit(Permission.bondsManage);
  protected readonly states = STATE_CHOICES;
  protected readonly kinds = KIND_FILTERS;
  protected readonly adding = signal(false);
  private readonly opened = signal(false);

  private readonly totalsResource = rxResource({ stream: () => this.api.totals() });
  protected readonly totals = computed<BondTotals | null>(() =>
    this.totalsResource.hasValue() ? this.totalsResource.value() : null,
  );

  /** Archived projects too: their bonds still lapse, so the register must be filterable by them. */
  private readonly projectList = rxResource({ stream: () => this.api.projects(true) });
  protected readonly projects = computed(() =>
    this.projectList.hasValue() ? this.projectList.value() : [],
  );

  /** "Posted by us: ₱5,000,000.00 in 2 bonds. Received: ₱300,000.00 in 1 bond." Each part only when there is something. */
  protected readonly summary = computed(() => {
    const t = this.totals();
    if (!t) return '';
    const parts: string[] = [];
    if (t.posted.activeCount > 0)
      parts.push(
        `Posted by us: ${formatMoney(t.posted.activeAmountMinor, t.currencyCode)} in ${count(t.posted.activeCount)}.`,
      );
    if (t.received.activeCount > 0)
      parts.push(
        `Received from firms: ${formatMoney(t.received.activeAmountMinor, t.currencyCode)} in ${count(t.received.activeCount)}.`,
      );
    return parts.join(' ');
  });

  /** Nothing is filtered and nothing is listed: the empty state carries the next step, so the toolbar would only repeat it. */
  protected readonly empty = computed(
    () =>
      !this.state.loadingFirst() &&
      !this.state.error() &&
      this.state.items().length === 0 &&
      !this.state.query.isFiltered(),
  );

  /** The filters stay folded until asked for, but open by themselves when the page was reached with one set. */
  protected readonly filtersOpen = computed(() => this.opened() || this.state.query.isFiltered());

  protected toggleFilters(): void {
    this.opened.set(!this.filtersOpen());
  }

  protected added(): void {
    this.adding.set(false);
    this.toast.success('Bond added.');
    this.state.reload();
    this.totalsResource.reload();
  }

  protected asState(value: string): BondState {
    return STATE_CHOICES.find((choice) => choice.value === value)?.value ?? 'Active';
  }

  protected asDirection(value: string): '' | BondDirection {
    return value === 'Posted' || value === 'Received' ? value : '';
  }

  protected asKind(value: string): '' | BondKind {
    return KIND_FILTERS.find((choice) => choice.value === value)?.value ?? '';
  }
}

function count(value: number): string {
  return value === 1 ? '1 bond' : `${value} bonds`;
}
