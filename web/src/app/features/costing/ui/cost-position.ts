import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { SessionState } from '../../../core/session/session-state';
import { formatMoney } from '../../../shared/presentation/money';
import { StatusChip } from '../../../shared/ui/status-chip/status-chip';
import { CommitmentPosition, Figures } from '../costing.models';
import { describeRemaining, describeUsed } from '../figures';
import { presentHealth } from '../health';
import { CommitmentLine } from './commitment-line';
import { UsedBar } from './used-bar';

/**
 * Budget, spent, remaining and how much is used, with the health chip and a thin bar. The dashboard shows one for all the
 * projects on screen and the Budget tab one for the open project, from the same shape, so the two can never word it differently.
 * Anything projected into it (for example the margins) sits below the figures.
 */
@Component({
  selector: 'app-cost-position',
  imports: [StatusChip, UsedBar, CommitmentLine],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dl class="position">
      <div>
        <dt>Budget</dt>
        <dd class="money">{{ view().budget }}</dd>
      </div>
      <div>
        <dt>Spent</dt>
        <dd class="money">{{ view().spent }}</dd>
      </div>
      <div>
        <dt>Remaining</dt>
        <dd class="money" [class.position__over]="view().remaining.over">
          {{ view().remaining.text }}
        </dd>
      </div>
      <div>
        <dt>Used</dt>
        <dd class="position__used">
          <span>{{ view().used }}</span>
          <app-status-chip
            [label]="view().health.label"
            [tone]="view().health.tone"
            [icon]="view().health.icon"
          />
        </dd>
      </div>
    </dl>
    @if (showBar()) {
      <app-used-bar
        class="position__bar"
        [usedBps]="figures().usedBps"
        [health]="figures().health"
      />
    }
    @if (commitments()) {
      <app-commitment-line [commitments]="commitments()" />
    }
    <ng-content />
  `,
  styles: `
    :host {
      display: grid;
      gap: var(--space-3);
    }

    .position {
      display: grid;
      gap: var(--space-3) var(--space-6);
      grid-template-columns: repeat(2, minmax(0, 1fr));
      margin: 0;
    }

    dt {
      font-size: var(--text-sm);
      color: var(--color-text-muted);
    }

    dd {
      margin: 2px 0 0;
      font-size: var(--text-lg);
      font-weight: 650;
      overflow-wrap: anywhere;
    }

    .position__over {
      color: var(--color-danger);
    }

    .position__used {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--space-1) var(--space-2);
    }

    @media (min-width: 48rem) {
      .position {
        grid-template-columns: repeat(4, minmax(0, 1fr));
      }
    }
  `,
})
export class CostPosition {
  readonly figures = input.required<Figures>();
  /** What is committed but not yet invoiced, with the forecast it gives; leave it out (or null) when nothing is. */
  readonly commitments = input<CommitmentPosition | null | undefined>(null);
  /** Hide the bar when nothing is budgeted and nothing is spent: an empty bar says nothing. */
  readonly hideEmptyBar = input(true);

  private readonly session = inject(SessionState);

  protected readonly view = computed(() => {
    const figures = this.figures();
    const currency = this.session.currencyCode();
    return {
      budget: formatMoney(figures.budgetMinor, currency),
      spent: formatMoney(figures.actualMinor, currency),
      remaining: describeRemaining(figures.remainingMinor, currency),
      used: describeUsed(figures.usedBps),
      health: presentHealth(figures.health),
    };
  });

  protected readonly showBar = computed(
    () => !(this.hideEmptyBar() && this.figures().health === 'NoActivity'),
  );
}
