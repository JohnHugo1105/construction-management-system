import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  signal,
  effect,
  untracked,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { formatDate } from '../../shared/presentation/business-time';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { Icon } from '../../shared/ui/icon/icon';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { StatutoryApi } from './statutory-api';
import { BASIS_LABEL, METHOD_LABEL } from './statutory-rules';
import { StatutoryScheduleListItem, StatutoryStatus } from './statutory.models';

/**
 * The contribution and tax tables (`/payroll/statutory`): first whether each agency is worked out for you, then the tables
 * themselves. People who see payroll read them; accountants and administrators add, change and retire them. Retired tables are
 * one link away.
 */
@Component({
  selector: 'app-statutory-page',
  imports: [RouterLink, PageHeader, ApiErrorAlert, Button, EmptyState, Icon, Skeleton, StatusChip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header
      title="Contribution and tax tables"
      backLabel="Payroll"
      [backLink]="['/payroll']"
      description="Payroll works out SSS, PhilHealth, Pag-IBIG and withholding tax from these tables. Enter them from the official schedules."
    />

    <div class="stack">
      @if (error(); as problem) {
        <app-api-error-alert
          [error]="problem"
          [allowRetry]="true"
          reloadLabel="Try again"
          (reload)="load()"
        />
      }

      @if (loading() && !status()) {
        <app-skeleton variant="card" [lines]="4" label="Loading tables" />
      } @else if (status(); as current) {
        <section class="card" aria-labelledby="status-heading">
          <h2 id="status-heading" class="card__title">What payroll works out</h2>
          <ul class="agencies">
            @for (agency of current.agencies; track agency.agency) {
              <li class="agencies__row">
                <app-status-chip
                  [label]="agency.state === 'Available' ? 'Worked out' : 'Needs setup'"
                  [tone]="agency.state === 'Available' ? 'success' : 'neutral'"
                  [icon]="agency.state === 'Available' ? 'check-circle' : 'minus-circle'"
                />
                <span class="agencies__text">
                  <strong>{{ agency.label }}</strong>
                  <span class="text-muted text-small">{{
                    agency.state === 'Available'
                      ? agency.message
                      : 'Entered by hand as a deduction until a table is set up.'
                  }}</span>
                </span>
              </li>
            }
          </ul>
        </section>

        <section aria-labelledby="tables-heading" class="stack">
          <div class="panel-head">
            <h2 id="tables-heading" class="section-title">Tables</h2>
            @if (canManage() && rows().length > 0) {
              <a appButton variant="primary" routerLink="/payroll/statutory/new">
                <app-icon name="plus" [size]="18" />
                Add a table
              </a>
            }
          </div>

          @if (rows().length === 0) {
            <app-empty-state
              icon="payroll"
              [title]="retired() ? 'No tables yet' : 'No tables in use'"
              [message]="
                canManage()
                  ? 'Add a table from the official schedule, with the date it takes effect. Until then payroll asks for these amounts by hand.'
                  : 'An accountant adds these tables. Until then, enter contributions and tax as deductions.'
              "
            >
              @if (canManage()) {
                <a appButton variant="primary" routerLink="/payroll/statutory/new">
                  <app-icon name="plus" [size]="18" />
                  Add a table
                </a>
              }
            </app-empty-state>
          } @else {
            <ul class="card-list">
              @for (row of rows(); track row.id) {
                <li class="row-card">
                  <div class="row-card__head">
                    <a
                      class="row-card__title row-link row-link--stretch"
                      [routerLink]="['/payroll/statutory', row.id]"
                    >
                      {{ row.agency }}
                    </a>
                    @if (row.chip; as chip) {
                      <app-status-chip [label]="chip.label" [tone]="chip.tone" [icon]="chip.icon" />
                    }
                  </div>
                  <p class="table__name">{{ row.name }}</p>
                  <p class="text-muted text-small">{{ row.detail }}</p>
                </li>
              }
            </ul>
          }

          <p>
            <a
              class="quiet-link"
              [routerLink]="[]"
              [queryParams]="{ retired: retired() ? null : '1' }"
            >
              {{ retired() ? 'Hide retired tables' : 'Show retired tables' }}
            </a>
          </p>
        </section>
      }
    </div>
  `,
  styles: `
    .agencies {
      display: grid;
      gap: var(--space-3);
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .agencies__row {
      display: grid;
      grid-template-columns: auto minmax(0, 1fr);
      align-items: start;
      gap: var(--space-3);
    }

    .agencies__text {
      display: grid;
      min-width: 0;
      overflow-wrap: anywhere;
    }

    .section-title {
      font-size: var(--text-lg);
      font-weight: 650;
    }

    .table__name {
      overflow-wrap: anywhere;
    }

    .quiet-link {
      display: inline-flex;
      align-items: center;
      min-height: 44px;
    }
  `,
})
export class StatutoryPage {
  /** `?retired=1` shows retired tables too (bound from the route). */
  readonly retiredParam = input<string | undefined>(undefined, { alias: 'retired' });

  private readonly api = inject(StatutoryApi);
  private readonly session = inject(SessionState);

  protected readonly canManage = this.session.permit(Permission.statutoryManage);
  protected readonly retired = computed(() => this.retiredParam() === '1');

  protected readonly status = signal<StatutoryStatus | null>(null);
  protected readonly items = signal<readonly StatutoryScheduleListItem[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<ApiError | null>(null);

  protected readonly rows = computed(() =>
    this.items().map((item) => ({
      id: item.id,
      agency: item.agencyLabel,
      name: item.name,
      detail: `From ${formatDate(item.effectiveFrom)} · ${METHOD_LABEL[item.method]} · ${BASIS_LABEL[item.periodBasis].toLowerCase()}`,
      chip:
        item.status === 'Retired'
          ? { label: 'Retired', tone: 'neutral' as const, icon: 'archive' as const }
          : item.inForce
            ? { label: 'In force', tone: 'success' as const, icon: 'check-circle' as const }
            : null,
    })),
  );

  constructor() {
    effect(() => {
      const retired = this.retired();
      untracked(() => void this.load(retired));
    });
  }

  async load(retired = this.retired()): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const [status, page] = await Promise.all([
        firstValueFrom(this.api.status()),
        firstValueFrom(this.api.list(retired)),
      ]);
      this.status.set(status);
      this.items.set(page.items);
    } catch (failure) {
      this.error.set(asApiError(failure));
    } finally {
      this.loading.set(false);
    }
  }
}
