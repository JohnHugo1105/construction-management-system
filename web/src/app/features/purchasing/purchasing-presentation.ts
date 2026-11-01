import { formatMoney } from '../../shared/presentation/money';
import { IconName } from '../../shared/ui/icon/icon';
import { ChipTone } from '../../shared/ui/status-chip/status-chip';
import { OpenPurchaseOrderLine, PurchaseOrderStatus } from './purchasing.models';

export interface StatusPresentation {
  readonly label: string;
  readonly tone: ChipTone;
  readonly icon: IconName;
  /** One short sentence for the order page: what the state means for the money. */
  readonly meaning: string;
}

/** How each order state looks and reads, in one place: the list, the order page and the project tab always agree. */
export const ORDER_STATUS: Record<PurchaseOrderStatus, StatusPresentation> = {
  Draft: {
    label: 'Draft',
    tone: 'neutral',
    icon: 'pencil',
    meaning: 'Not approved yet, so nothing is committed.',
  },
  Approved: {
    label: 'Approved',
    tone: 'info',
    icon: 'check-circle',
    meaning: 'What has not been invoiced yet counts as committed cost.',
  },
  Closed: {
    label: 'Closed',
    tone: 'neutral',
    icon: 'lock',
    meaning: 'Closed early. What was not invoiced is no longer committed.',
  },
  Cancelled: {
    label: 'Cancelled',
    tone: 'warning',
    icon: 'minus-circle',
    meaning: 'Called off. Nothing is committed.',
  },
};

/** The word for a status this client does not know, so a new one never breaks a page. */
export function presentStatus(status: string): StatusPresentation {
  return Object.hasOwn(ORDER_STATUS, status)
    ? ORDER_STATUS[status as PurchaseOrderStatus]
    : { label: status, tone: 'neutral', icon: 'info', meaning: '' };
}

/** The status chips above the orders list. "All" leaves the status out of the question. */
export const STATUS_FILTERS = ['All', 'Draft', 'Approved', 'Closed', 'Cancelled'] as const;
export type StatusFilter = (typeof STATUS_FILTERS)[number];

/** "PO-0003 line 2, Rebar 16 mm, ₱3,000.00 remaining": one line of the "Against a purchase order" choice. */
export function openLineLabel(line: OpenPurchaseOrderLine, currencyCode: string): string {
  const remaining =
    line.remainingGrossMinor > 0
      ? `${formatMoney(line.remainingGrossMinor, currencyCode)} remaining`
      : 'fully invoiced';
  return `${line.orderNumber} line ${line.lineNumber}, ${line.description}, ${remaining}`;
}

/**
 * What a line's invoices come to against what was ordered, in plain words. On a closed order what was not invoiced was released,
 * so it is not "remaining" any more.
 */
export function invoicedSummary(
  line: { invoicedGrossMinor: number; remainingGrossMinor: number; overByGrossMinor: number },
  currencyCode: string,
  closed = false,
): string {
  if (line.overByGrossMinor > 0)
    return `Over by ${formatMoney(line.overByGrossMinor, currencyCode)}`;
  if (closed) {
    return line.remainingGrossMinor > 0
      ? `${formatMoney(line.remainingGrossMinor, currencyCode)} released`
      : 'Fully invoiced';
  }
  if (line.invoicedGrossMinor === 0) return 'Not invoiced yet';
  return line.remainingGrossMinor === 0
    ? 'Fully invoiced'
    : `${formatMoney(line.remainingGrossMinor, currencyCode)} remaining`;
}

/** The firm's kind in words: "Supplier", "Subcontractor" or "Supplier and subcontractor". */
export function kindLabel(firm: { isSupplier: boolean; isSubcontractor: boolean }): string {
  if (firm.isSupplier && firm.isSubcontractor) return 'Supplier and subcontractor';
  return firm.isSubcontractor ? 'Subcontractor' : 'Supplier';
}
