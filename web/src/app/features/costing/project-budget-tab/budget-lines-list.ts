import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { SessionState } from '../../../core/session/session-state';
import { formatDate } from '../../../shared/presentation/business-time';
import { formatMoney } from '../../../shared/presentation/money';
import { Button } from '../../../shared/ui/button/button';
import { StatusChip } from '../../../shared/ui/status-chip/status-chip';
import { Viewport } from '../../../shared/ui/viewport/viewport';
import {
  describeClassification,
  formatQuantity,
  itemTypeLabel,
  vatTreatmentShort,
} from '../costing-labels';
import { BudgetLine } from '../costing.models';
import { injectMinWidth } from '../ui/container-width';
import { ScrollRegion } from '../ui/scroll-region';

/** One budget line with every figure already written the way it is shown. */
export interface BudgetLineRow {
  readonly line: BudgetLine;
  readonly about: string;
  readonly quantity: string;
  readonly unitCost: string;
  readonly vatNote: string;
  readonly net: string;
  readonly vat: string;
  readonly gross: string;
  readonly date: string;
  readonly removed: boolean;
}

/**
 * The budget lines of one page: a table on wide screens (the description stays put while the figures scroll sideways) and cards
 * on phones. It only shows what it is given and reports Edit and Remove; the Budget tab owns the data and the dialogs.
 */
@Component({
  selector: 'app-budget-lines-list',
  imports: [Button, StatusChip, ScrollRegion],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './budget-lines-list.html',
  styleUrl: './budget-lines-list.scss',
})
export class BudgetLinesList {
  readonly lines = input.required<readonly BudgetLine[]>();
  /** Show Edit and Remove. The tab turns this off for people who cannot write and for archived projects. */
  readonly canWrite = input(false);

  readonly edit = output<BudgetLine>();
  readonly remove = output<BudgetLine>();

  protected readonly viewport = inject(Viewport);
  /** A table needs about this much room; with less (a tablet, or a laptop with the side rail) the lines are cards. */
  protected readonly roomForTable = injectMinWidth(50);
  private readonly session = inject(SessionState);

  protected readonly rows = computed<BudgetLineRow[]>(() => {
    const currency = this.session.currencyCode();
    return this.lines().map((line) => ({
      line,
      about: [itemTypeLabel(line.itemType), describeClassification(line), line.subProjectName ?? '']
        .filter(Boolean)
        .join(' · '),
      quantity: `${formatQuantity(line.quantity)} ${line.unit}`,
      unitCost: formatMoney(line.unitCostMinor, currency),
      vatNote: vatTreatmentShort(line.vatTreatment),
      net: formatMoney(line.netMinor, currency),
      vat: formatMoney(line.vatMinor, currency),
      gross: formatMoney(line.grossMinor, currency),
      date: formatDate(line.entryDate),
      removed: line.status === 'Removed',
    }));
  });
}
