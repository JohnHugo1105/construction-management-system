import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { SessionState } from '../../../core/session/session-state';
import { formatDate } from '../../../shared/presentation/business-time';
import { formatMoney } from '../../../shared/presentation/money';
import { Button } from '../../../shared/ui/button/button';
import { StatusChip } from '../../../shared/ui/status-chip/status-chip';
import { Viewport } from '../../../shared/ui/viewport/viewport';
import {
  OriginPresentation,
  describeClassification,
  itemTypeLabel,
  presentOrigin,
  systemMadeNote,
} from '../costing-labels';
import { CostEntry } from '../costing.models';
import { injectMinWidth } from '../ui/container-width';
import { ScrollRegion } from '../ui/scroll-region';

/** One cost entry with every figure already written the way it is shown. */
export interface CostEntryRow {
  readonly entry: CostEntry;
  /** "Cebu Plumbing Supply · Invoice CPS-7781". */
  readonly source: string;
  /** "Equipment · 15200 Process Piping". */
  readonly about: string;
  readonly origin: OriginPresentation;
  readonly net: string;
  readonly vat: string;
  readonly gross: string;
  readonly date: string;
  readonly voided: boolean;
  /** Where to correct a system-made entry; empty for a manual one. */
  readonly systemNote: string;
  readonly canEdit: boolean;
  readonly canVoid: boolean;
}

/**
 * The cost entries of one page: a table on wide screens and cards on phones. An entry made by payroll or stock says where to
 * correct it and has no buttons; a manual one can be edited or voided by people who may write. It reports Edit and Void; the
 * Costs tab owns the data and the dialogs.
 */
@Component({
  selector: 'app-cost-entries-list',
  imports: [Button, StatusChip, ScrollRegion],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './cost-entries-list.html',
  styleUrl: './cost-entries-list.scss',
})
export class CostEntriesList {
  readonly entries = input.required<readonly CostEntry[]>();
  /** Show Edit and Void. The tab turns this off for people who cannot write and for archived projects. */
  readonly canWrite = input(false);

  readonly edit = output<CostEntry>();
  readonly voidEntry = output<CostEntry>();

  protected readonly viewport = inject(Viewport);
  /** A table needs about this much room; with less (a tablet, or a laptop with the side rail) the entries are cards. */
  protected readonly roomForTable = injectMinWidth(56);
  private readonly session = inject(SessionState);

  protected readonly rows = computed<CostEntryRow[]>(() => {
    const currency = this.session.currencyCode();
    const canWrite = this.canWrite();
    return this.entries().map((entry) => {
      const voided = entry.status === 'Voided';
      const manual = entry.origin === 'Manual';
      return {
        entry,
        source: [entry.sourceDealer, entry.invoiceNumber ? `Invoice ${entry.invoiceNumber}` : '']
          .filter(Boolean)
          .join(' · '),
        about: [
          itemTypeLabel(entry.itemType),
          describeClassification(entry),
          entry.subProjectName ?? '',
        ]
          .filter(Boolean)
          .join(' · '),
        origin: presentOrigin(entry.origin),
        net: formatMoney(entry.netMinor, currency),
        vat: formatMoney(entry.vatMinor, currency),
        gross: formatMoney(entry.grossMinor, currency),
        date: formatDate(entry.entryDate),
        voided,
        systemNote: !manual && !voided ? systemMadeNote(entry.origin) : '',
        canEdit: canWrite && entry.canEdit && !voided,
        canVoid: canWrite && manual && !voided,
      };
    });
  });
}
