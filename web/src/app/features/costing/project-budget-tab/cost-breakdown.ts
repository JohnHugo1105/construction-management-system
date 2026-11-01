import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { SessionState } from '../../../core/session/session-state';
import { formatMoney } from '../../../shared/presentation/money';
import { Icon } from '../../../shared/ui/icon/icon';
import { StatusChip } from '../../../shared/ui/status-chip/status-chip';
import { Viewport } from '../../../shared/ui/viewport/viewport';
import { CostSummary, Figures } from '../costing.models';
import { describeRemaining, describeUsed } from '../figures';
import { presentHealth } from '../health';
import { injectMinWidth } from '../ui/container-width';
import { ScrollRegion } from '../ui/scroll-region';
import { SegmentedControl } from '../ui/segmented-control';
import { UsedBar } from '../ui/used-bar';
import {
  BreakdownRow,
  BreakdownView,
  availableViews,
  breakdownRows,
  startsOpen,
} from './breakdown-rows';

interface FigureView {
  readonly budget: string;
  readonly spent: string;
  readonly remaining: string;
  readonly over: boolean;
  readonly used: string;
  readonly health: ReturnType<typeof presentHealth>;
}

interface RowView {
  readonly row: BreakdownRow;
  readonly figures: FigureView;
  readonly open: boolean;
  readonly children: readonly { readonly row: BreakdownRow; readonly figures: FigureView }[];
}

/**
 * Where a project's money goes: by division (each opens to its work-breakdown codes), or by sub-project or by type. Divisions
 * with something over budget, unbudgeted or near its limit start open, and say so on their row, so a problem is never folded
 * away. A table on wide screens and cards on phones.
 */
@Component({
  selector: 'app-cost-breakdown',
  imports: [Icon, StatusChip, UsedBar, ScrollRegion, SegmentedControl],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './cost-breakdown.html',
  styleUrl: './cost-breakdown.scss',
})
export class CostBreakdown {
  readonly summary = input.required<CostSummary>();

  protected readonly viewport = inject(Viewport);
  /** Six columns need about this much room; with less the divisions are cards, so nothing hides behind a sideways scroll. */
  protected readonly roomForTable = injectMinWidth(60);
  private readonly session = inject(SessionState);

  protected readonly choices = computed(() => availableViews(this.summary()));
  private readonly chosen = signal<BreakdownView>('division');
  /** The view on show: the one picked, unless the project no longer offers it. */
  protected readonly view = computed(() => {
    const picked = this.chosen();
    return this.choices().some((choice) => choice.value === picked) ? picked : 'division';
  });

  /** Divisions the person opened or closed by hand; the rest follow the default (open when something needs a look). */
  private readonly overrides = signal<ReadonlyMap<string, boolean>>(new Map());

  protected readonly rows = computed<RowView[]>(() => {
    const currency = this.session.currencyCode();
    const view = this.view();
    return breakdownRows(this.summary(), view).map((row) => ({
      row,
      figures: this.describe(row.figures, currency),
      open: this.overrides().get(row.key) ?? startsOpen(row),
      children: row.children.map((child) => ({
        row: child,
        figures: this.describe(child.figures, currency),
      })),
    }));
  });

  protected readonly heading = computed(() => {
    switch (this.view()) {
      case 'division':
        return 'Division';
      case 'subProject':
        return 'Sub-project';
      case 'itemType':
        return 'Type';
    }
  });

  protected show(view: BreakdownView): void {
    this.chosen.set(view);
  }

  protected toggle(key: string, currentlyOpen: boolean): void {
    this.overrides.update((map) => new Map(map).set(key, !currentlyOpen));
  }

  private describe(figures: Figures, currency: string): FigureView {
    const remaining = describeRemaining(figures.remainingMinor, currency);
    return {
      budget: formatMoney(figures.budgetMinor, currency),
      spent: formatMoney(figures.actualMinor, currency),
      remaining: remaining.text,
      over: remaining.over,
      used: describeUsed(figures.usedBps),
      health: presentHealth(figures.health),
    };
  }
}
