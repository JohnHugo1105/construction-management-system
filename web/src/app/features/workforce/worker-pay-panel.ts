import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ToastService } from '../../core/notifications/toast-service';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { formatDate, todayIn } from '../../shared/presentation/business-time';
import { formatMoney } from '../../shared/presentation/money';
import { Button } from '../../shared/ui/button/button';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { ChangeRateDialog } from './change-rate-dialog';
import { rateInForce } from './rate-change';
import { WorkerDetailState } from './worker-detail-state';
import { WorkerStatutoryDialog, statutoryWords } from './worker-statutory';
import { RATE_CATEGORY_LABEL, RATE_UNIT_LABEL } from './worker-status';
import { WorkerDetail } from './workforce.models';

/**
 * Pay is confidential. This panel shows the rate and its history only when the API sent them (it does that for people who hold
 * payroll.read) and offers a change only to people who also manage workers. Everyone else sees one line saying pay is limited to
 * payroll staff, and no figure anywhere.
 */
@Component({
  selector: 'app-worker-pay-panel',
  imports: [Button, ChangeRateDialog, WorkerStatutoryDialog],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="card" aria-labelledby="pay-heading">
      <div class="panel-head">
        <h2 id="pay-heading" class="card__title">Pay</h2>
        @if (canChange()) {
          <button appButton type="button" (click)="changing.set(true)">Change pay rate</button>
        }
      </div>

      @if (!showsPay()) {
        <p class="text-muted">Pay details are limited to payroll staff.</p>
      } @else {
        @if (now(); as current) {
          <p class="pay__rate">
            <span class="money">{{ current.amount }}</span> {{ current.unit }}
          </p>
          <p class="text-muted text-small">{{ current.designation }} · since {{ current.since }}</p>
        }
        @if (upcoming(); as next) {
          <p class="pay__next text-small">
            From {{ next.since }}: {{ next.designation }},
            <span class="money">{{ next.amount }}</span>
            {{ next.unit }}
          </p>
        }

        @if (contributions(); as words) {
          <div class="pay__statutory">
            <p class="text-small"><span class="text-muted">Contributions:</span> {{ words }}</p>
            @if (canChangeContributions()) {
              <button
                appButton
                variant="ghost"
                type="button"
                (click)="editingContributions.set(true)"
              >
                Change contributions
              </button>
            }
          </div>
        }

        @if (history().length > 1) {
          <details class="pay__history">
            <summary>Pay history ({{ history().length }})</summary>
            @if (viewport.isTablet()) {
              <div class="table-wrap" role="region" aria-label="Pay history" tabindex="0">
                <table class="data-table">
                  <thead>
                    <tr>
                      <th scope="col">Starts</th>
                      <th scope="col">Designation</th>
                      <th scope="col" class="num">Rate</th>
                      <th scope="col">Change</th>
                      <th scope="col">Reason</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (line of history(); track line.id) {
                      <tr>
                        <th scope="row">{{ line.starts }}</th>
                        <td>{{ line.designation }}</td>
                        <td class="num">{{ line.amount }} {{ line.unit }}</td>
                        <td>{{ line.category }}</td>
                        <td>{{ line.reason }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            } @else {
              <ul class="history">
                @for (line of history(); track line.id) {
                  <li>
                    <span class="history__what"
                      >{{ line.designation }}: <span class="money">{{ line.amount }}</span>
                      {{ line.unit }}</span
                    >
                    <span class="text-muted text-small"
                      >{{ line.category }} · {{ line.starts }}</span
                    >
                    @if (line.reason) {
                      <span class="text-muted text-small">{{ line.reason }}</span>
                    }
                  </li>
                }
              </ul>
            }
          </details>
        }
      }
    </section>

    @if (editingContributions()) {
      <app-worker-statutory-dialog
        [worker]="worker()"
        (changed)="contributionsChanged($event)"
        (dismissed)="editingContributions.set(false)"
        (reloaded)="reload()"
      />
    }

    @if (changing()) {
      <app-change-rate-dialog
        [worker]="worker()"
        (changed)="changed($event)"
        (dismissed)="changing.set(false)"
        (reloaded)="reload()"
      />
    }
  `,
  styles: `
    :host {
      display: block;
    }

    .pay__rate {
      font-size: var(--text-xl);
      font-weight: 650;
    }

    .pay__next {
      margin-top: var(--space-2);
      color: var(--color-info);
      font-weight: 600;
    }

    .pay__statutory {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--space-2);
      margin-top: var(--space-3);
    }

    .pay__history {
      margin-top: var(--space-4);
      padding-top: var(--space-2);
      border-top: 1px solid var(--color-border);
    }

    .pay__history summary {
      display: flex;
      align-items: center;
      min-height: var(--touch-target);
      font-weight: 600;
      cursor: pointer;
    }

    .history {
      display: grid;
      gap: var(--space-3);
      margin: var(--space-2) 0 0;
      padding: 0;
      list-style: none;
    }

    .history li {
      display: grid;
      gap: 2px;
    }

    .history__what {
      font-weight: 600;
    }
  `,
})
export class WorkerPayPanel {
  private readonly state = inject(WorkerDetailState);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);
  protected readonly viewport = inject(Viewport);

  protected readonly worker = this.state.worker;
  protected readonly showsPay = this.state.showsPay;
  protected readonly changing = signal(false);

  /** Changing pay needs the right to manage workers, and a response that carried pay. A worker who has left is reinstated first. */
  protected readonly canChange = computed(
    () =>
      this.session.can(Permission.workforceManage) &&
      this.state.showsPay() &&
      !['Resigned', 'Retired', 'Terminated'].includes(this.worker().status),
  );

  protected readonly now = computed(() => {
    const worker = this.worker();
    const line = rateInForce(worker.rates, todayIn(this.session.timeZoneId()));
    if (!line) return null;
    return {
      amount: formatMoney(line.rateMinor, this.session.currencyCode()),
      unit: RATE_UNIT_LABEL[line.rateUnit],
      designation: line.designation,
      since: formatDate(line.effectiveFrom),
    };
  });

  /** A change that has been recorded but has not started yet, so the rate shown above is not mistaken for the last word. */
  protected readonly upcoming = computed(() => {
    const today = todayIn(this.session.timeZoneId());
    const next = [...this.worker().rates]
      .filter((line) => line.effectiveFrom > today)
      .sort((a, b) => a.effectiveFrom.localeCompare(b.effectiveFrom))[0];
    if (!next) return null;
    return {
      since: formatDate(next.effectiveFrom),
      designation: next.designation,
      amount: formatMoney(next.rateMinor, this.session.currencyCode()),
      unit: RATE_UNIT_LABEL[next.rateUnit],
    };
  });

  /** Every line of pay history, newest first, worded for a person. */
  protected readonly history = computed(() =>
    [...this.worker().rates]
      .sort((a, b) => b.effectiveFrom.localeCompare(a.effectiveFrom))
      .map((line) => ({
        id: line.id,
        starts: formatDate(line.effectiveFrom),
        designation: line.designation,
        amount: formatMoney(line.rateMinor, this.session.currencyCode()),
        unit: RATE_UNIT_LABEL[line.rateUnit],
        category: RATE_CATEGORY_LABEL[line.category],
        // "Hired" says the same as the change shown beside it, so only a reason a person wrote is shown.
        reason: line.category === 'Initial' ? '' : (line.reason ?? ''),
      })),
  );

  protected readonly editingContributions = signal(false);

  /** The contributions the worker pays, in words; only sent to people who may see pay. */
  protected readonly contributions = computed(() => {
    const flags = this.worker().statutory;
    return flags ? statutoryWords(flags) : null;
  });

  /** Changing contributions needs the right to manage workers; a worker who has left can still be in a draft payroll. */
  protected readonly canChangeContributions = computed(
    () => this.session.can(Permission.workforceManage) && this.worker().statutory !== null,
  );

  protected contributionsChanged(updated: WorkerDetail): void {
    this.editingContributions.set(false);
    this.state.replace(updated);
    this.toast.success('Contributions saved.');
  }

  protected changed(updated: WorkerDetail): void {
    this.changing.set(false);
    this.state.replace(updated);
    this.toast.success('Pay rate changed.');
  }

  protected reload(): void {
    this.changing.set(false);
    this.editingContributions.set(false);
    void this.state.reload();
  }
}
