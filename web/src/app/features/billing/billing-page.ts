import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { DEFAULT_PAGE_SIZE } from '../../core/api/paged-result';
import { SessionState } from '../../core/session/session-state';
import { formatMoney } from '../../shared/presentation/money';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Pager } from '../../shared/ui/pager/pager';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { BillingApi } from './billing-api';
import { Receivables } from './billing.models';

/**
 * What clients still owe, company-wide: the totals, then one row per project with money to collect or retention held, overdue
 * first. A row's one action opens the project's billing, where bills are prepared and payments recorded. Every figure is the
 * server's.
 */
@Component({
  selector: 'app-billing-page',
  imports: [RouterLink, PageHeader, ApiErrorAlert, Button, EmptyState, Pager, Skeleton, StatusChip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Billing" description="What clients still owe, overdue first." />

    @if (error(); as problem) {
      <app-api-error-alert
        [error]="problem"
        [allowRetry]="true"
        reloadLabel="Try again"
        (reload)="load()"
      />
    }

    @if (loading()) {
      <app-skeleton variant="card" [lines]="3" label="Loading what clients owe" />
    } @else if (view(); as info) {
      @if (info.rows.length === 0) {
        <app-empty-state
          title="Nothing to collect"
          message="Every issued bill is paid and no retention is held. Bills are prepared on each project's Billing tab."
          icon="receipt"
        >
          <a appButton routerLink="/projects">Open projects</a>
        </app-empty-state>
      } @else {
        <div class="stack">
          <dl class="totals">
            <div class="totals__item totals__item--main">
              <dt>Still to collect</dt>
              <dd class="money">{{ info.outstanding }}</dd>
            </div>
            <div class="totals__item">
              <dt>Overdue</dt>
              <dd class="money">{{ info.overdue }}</dd>
            </div>
            <div class="totals__item">
              <dt>Retention held</dt>
              <dd class="money">{{ info.retention }}</dd>
            </div>
          </dl>

          @if (viewport.isTablet()) {
            <div
              class="table-wrap"
              role="region"
              aria-label="What each project's client owes"
              tabindex="0"
            >
              <table class="data-table">
                <thead>
                  <tr>
                    <th scope="col">Project</th>
                    <th scope="col" class="num">Still to collect</th>
                    <th scope="col" class="num">Retention held</th>
                    <th scope="col"><span class="visually-hidden">Open</span></th>
                  </tr>
                </thead>
                <tbody>
                  @for (row of info.rows; track row.id) {
                    <tr>
                      <th scope="row">
                        {{ row.name }}
                        @if (row.overdue) {
                          <app-status-chip [label]="row.overdue" tone="danger" icon="clock" />
                        }
                      </th>
                      <td class="num">{{ row.outstanding }}</td>
                      <td class="num">{{ row.retention }}</td>
                      <td class="actions">
                        <a
                          appButton
                          variant="ghost"
                          [routerLink]="row.link"
                          [attr.aria-label]="'Open billing of ' + row.name"
                          >Open</a
                        >
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          } @else {
            <ul class="card-list">
              @for (row of info.rows; track row.id) {
                <li class="row-card">
                  <div class="row-card__head">
                    <a class="row-card__title row-link row-link--stretch" [routerLink]="row.link">{{
                      row.name
                    }}</a>
                  </div>
                  @if (row.overdue) {
                    <app-status-chip [label]="row.overdue" tone="danger" icon="clock" />
                  }
                  <dl class="row-figures">
                    <div>
                      <dt>Still to collect</dt>
                      <dd class="money">{{ row.outstanding }}</dd>
                    </div>
                    <div>
                      <dt>Retention held</dt>
                      <dd class="money">{{ row.retention }}</dd>
                    </div>
                  </dl>
                </li>
              }
            </ul>
          }
          <app-pager
            [page]="page()"
            [pageSize]="info.pageSize"
            [total]="info.total"
            noun="projects"
            (pageChange)="page.set($event)"
          />
        </div>
      }
    }
  `,
  styles: `
    :host {
      display: block;
    }

    .totals {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: var(--space-3);
      margin: 0;
    }

    .totals__item {
      display: grid;
      gap: 2px;
      min-width: 0;
      padding: var(--space-3) var(--space-4);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      background: var(--color-surface);

      dt {
        font-size: var(--text-sm);
        color: var(--color-text-muted);
      }

      dd {
        margin: 0;
        font-size: var(--text-lg);
        font-weight: 650;
        overflow-wrap: anywhere;
      }
    }

    .totals__item--main {
      grid-column: 1 / -1;

      dd {
        font-size: var(--text-2xl);
      }
    }

    th app-status-chip {
      display: inline-block;
      margin-left: var(--space-2);
    }

    .row-figures {
      display: flex;
      flex-wrap: wrap;
      justify-content: space-between;
      gap: var(--space-2) var(--space-4);
      margin: 0;

      dt {
        font-size: var(--text-sm);
        color: var(--color-text-muted);
      }

      dd {
        margin: 0;
        font-weight: 600;
      }
    }

    @media (min-width: 48rem) {
      .totals {
        grid-template-columns: repeat(3, minmax(0, 1fr));
      }

      .totals__item--main {
        grid-column: auto;
      }
    }
  `,
})
export class BillingPage {
  private readonly api = inject(BillingApi);
  private readonly session = inject(SessionState);
  protected readonly viewport = inject(Viewport);

  protected readonly receivables = signal<Receivables | null>(null);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly loading = signal(true);
  protected readonly page = signal(1);

  protected readonly view = computed(() => {
    const data = this.receivables();
    if (!data) return null;
    const money = (minor: number): string => formatMoney(minor, this.session.currencyCode());
    return {
      outstanding: money(data.outstandingMinor),
      overdue: money(data.overdueMinor),
      retention: money(data.retentionHeldMinor),
      rows: data.rows.items.map((row) => ({
        id: row.projectId,
        name: row.projectName,
        link: ['/projects', row.projectId, 'billing'],
        outstanding: money(row.outstandingMinor),
        overdue: row.overdueMinor > 0 ? `${money(row.overdueMinor)} overdue` : '',
        retention: money(row.retentionHeldMinor),
      })),
      total: data.rows.total,
      pageSize: data.rows.pageSize,
    };
  });

  constructor() {
    effect(() => {
      const page = this.page();
      untracked(() => void this.fetch(page));
    });
  }

  protected load(): void {
    void this.fetch(this.page());
  }

  private async fetch(page: number): Promise<void> {
    this.loading.set(this.receivables() === null);
    this.error.set(null);
    try {
      this.receivables.set(await firstValueFrom(this.api.receivables(page, DEFAULT_PAGE_SIZE)));
    } catch (failure) {
      this.error.set(asApiError(failure));
    } finally {
      this.loading.set(false);
    }
  }
}
