import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { SessionState } from '../../../core/session/session-state';
import { formatMoney } from '../../../shared/presentation/money';
import { StatusChip } from '../../../shared/ui/status-chip/status-chip';
import { CommitmentPosition, Health } from '../costing.models';

/**
 * What the chip says for each judgement of "spent and committed" against the budget. Only one that needs a look gets a chip; "on track" is the
 * quiet default. Worded as spent plus committed, not "forecast", so it is never mistaken for the progress forecast (cost to finish).
 */
const FORECAST_WORDS: Partial<Record<Health, string>> = {
  OverBudget: 'Over budget once orders are paid',
  NearLimit: 'Near the limit once orders are paid',
  Unbudgeted: 'Committed with no budget',
};

/**
 * "Committed ₱x · Spent and committed ₱y" for a project or for all projects: what is promised but not yet invoiced, and what the
 * project has cost plus what it is already committed to. Shows nothing when nothing is committed, so a project without open orders carries no extra figures. The
 * dashboard row and the Budget tab use this one component from the same server calculation, so they can never word it differently.
 */
@Component({
  selector: 'app-commitment-line',
  imports: [StatusChip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (view(); as v) {
      <p class="commitments">
        <span class="nowrap"
          >Committed <span class="money">{{ v.committed }}</span></span
        >
        <span class="nowrap"
          >Spent and committed <span class="money">{{ v.forecast }}</span></span
        >
        @if (v.chip; as chip) {
          <app-status-chip [label]="chip.label" [tone]="chip.tone" [icon]="chip.icon" />
        }
      </p>
    }
  `,
  styles: `
    :host {
      display: block;
    }

    .commitments {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--space-1) var(--space-4);
      margin: 0;
      font-size: var(--text-sm);
    }

    .nowrap {
      white-space: nowrap;
    }
  `,
})
export class CommitmentLine {
  readonly commitments = input<CommitmentPosition | null | undefined>(null);

  private readonly session = inject(SessionState);

  protected readonly view = computed(() => {
    const position = this.commitments();
    if (!position) return null;
    const currency = this.session.currencyCode();
    const words = FORECAST_WORDS[position.forecastHealth];
    return {
      committed: formatMoney(position.committedMinor, currency),
      forecast: formatMoney(position.forecastMinor, currency),
      // A forecast is a warning, not a fact: the warning tone, never the danger one that over-spent budgets use.
      chip: words ? { label: words, tone: 'warning' as const, icon: 'warning' as const } : null,
    };
  });
}
