import { formatMoney } from '../../shared/presentation/money';
import { ChipTone } from '../../shared/ui/status-chip/status-chip';
import { IconName } from '../../shared/ui/icon/icon';
import {
  BudgetBaseline,
  ChangeOrderLineKind,
  ChangeOrderReason,
  ChangeOrderStatus,
  GovernmentLimit,
} from './change-orders.models';

export const REASON_LABELS: Record<ChangeOrderReason, string> = {
  ClientRequest: 'Client request',
  DesignChange: 'Design change',
  SiteCondition: 'Site condition',
  ErrorOrOmission: 'Error or omission',
  Other: 'Other',
};

export const REASON_CHOICES = (Object.keys(REASON_LABELS) as ChangeOrderReason[]).map((value) => ({
  value,
  label: REASON_LABELS[value],
}));

export const LINE_KIND_LABELS: Record<ChangeOrderLineKind, string> = {
  Add: 'Add',
  Revise: 'Revise',
  Omit: 'Omit',
};

export interface StatusLook {
  readonly label: string;
  readonly tone: ChipTone;
  readonly icon: IconName;
}

/** How a change order's state reads. A voided draft or submitted order was discarded; only an approved one was reversed. */
export function statusLook(
  status: ChangeOrderStatus,
  voidedFrom: ChangeOrderStatus | null = null,
): StatusLook {
  switch (status) {
    case 'Draft':
      return { label: 'Draft', tone: 'neutral', icon: 'pencil' };
    case 'Submitted':
      return { label: 'Waiting for approval', tone: 'info', icon: 'clock' };
    case 'Approved':
      return { label: 'Approved', tone: 'success', icon: 'check-circle' };
    case 'Rejected':
      return { label: 'Rejected', tone: 'danger', icon: 'error' };
    case 'Voided':
      return voidedFrom === 'Approved'
        ? { label: 'Voided', tone: 'warning', icon: 'restore' }
        : { label: 'Discarded', tone: 'neutral', icon: 'archive' };
  }
}

/**
 * The one line a government project shows when added work passes the agency's limit, or null. The server decides whether it
 * passes; approval is still allowed (the agency's approval is a person's act outside the system).
 */
export function governmentLimitNotice(limit: GovernmentLimit | null | undefined): string | null {
  return limit?.applies && limit.exceeds
    ? 'Added work on this government project passes 10% of the original contract price. Check that the agency has approved it.'
    : null;
}

/** A signed change of money: "+₱1,000.00", "-₱560.00", or "No change". */
export function signedMoney(minor: number, currencyCode: string): string {
  if (minor === 0) return 'No change';
  return `${minor > 0 ? '+' : ''}${formatMoney(minor, currencyCode)}`;
}

/** The parts of an order's effect, each kept whole on screen: ["Budget +₱999.99", "Price +₱1,500.00"]. */
export function effectParts(
  budgetGrossMinor: number,
  priceDeltaMinor: number,
  currencyCode: string,
): string[] {
  const parts: string[] = [];
  if (budgetGrossMinor !== 0) parts.push(`Budget ${signedMoney(budgetGrossMinor, currencyCode)}`);
  if (priceDeltaMinor !== 0) parts.push(`Price ${signedMoney(priceDeltaMinor, currencyCode)}`);
  return parts.length === 0 ? ['No change to the budget or the price'] : parts;
}

/** The order's effect in one short phrase: "Budget +₱999.99 · Price +₱1,500.00". */
export function effectSummary(
  budgetGrossMinor: number,
  priceDeltaMinor: number,
  currencyCode: string,
): string {
  return effectParts(budgetGrossMinor, priceDeltaMinor, currencyCode).join(' · ');
}

export interface BaselineRow {
  readonly label: string;
  readonly original: string;
  readonly changes: string;
  readonly revised: string;
}

export interface BaselineView {
  readonly rows: readonly BaselineRow[];
  /** One line of plain words under the figures. */
  readonly sentence: string;
  /** Submitted orders waiting for a decision, or null when there are none. */
  readonly pending: string | null;
  readonly reconciles: boolean;
}

/** The baseline card's figures, all with VAT, written the way they are shown. Only a locked budget has one. */
export function baselineView(baseline: BudgetBaseline, currencyCode: string): BaselineView {
  const money = (minor: number | null) =>
    minor === null ? 'None' : formatMoney(minor, currencyCode);
  const rows: BaselineRow[] = [
    {
      label: 'Budget',
      original: money(baseline.originalBudgetGrossMinor),
      changes: signedMoney(baseline.approvedBudgetGrossMinor, currencyCode),
      revised: money(baseline.revisedBudgetGrossMinor),
    },
  ];
  if (baseline.originalContractPriceMinor !== null || baseline.revisedContractPriceMinor !== null)
    rows.push({
      label: 'Contract price',
      original: money(baseline.originalContractPriceMinor),
      changes: signedMoney(baseline.approvedContractPriceDeltaMinor, currencyCode),
      revised: money(baseline.revisedContractPriceMinor),
    });

  const approved = baseline.approvedCount;
  const sentence =
    approved === 0
      ? 'No change order has been approved yet, so the budget is still the original.'
      : `${approved} approved change order${approved === 1 ? '' : 's'} explain${approved === 1 ? 's' : ''} the difference from the original.`;
  const pending =
    baseline.pendingCount === 0
      ? null
      : `${baseline.pendingCount} change order${baseline.pendingCount === 1 ? '' : 's'} waiting for approval: ${effectSummary(
          baseline.pendingBudgetGrossMinor,
          baseline.pendingContractPriceDeltaMinor,
          currencyCode,
        )} if approved.`;
  return { rows, sentence, pending, reconciles: baseline.reconciles };
}

/** What the signed-in person may do with an order in its current state. */
export interface ChangeOrderActions {
  readonly edit: boolean;
  readonly submit: boolean;
  readonly approve: boolean;
  readonly reject: boolean;
  readonly void: boolean;
  readonly discard: boolean;
}

export function allowedActions(
  status: ChangeOrderStatus,
  can: { readonly manage: boolean; readonly approve: boolean },
  archived: boolean,
): ChangeOrderActions {
  const open = !archived;
  return {
    edit: open && can.manage && status === 'Draft',
    submit: open && can.manage && status === 'Draft',
    approve: open && can.approve && status === 'Submitted',
    reject: open && can.approve && status === 'Submitted',
    void: open && can.approve && status === 'Approved',
    discard: open && can.manage && (status === 'Draft' || status === 'Submitted'),
  };
}
