import { Params } from '@angular/router';
import { Permission } from '../../core/session/permissions';
import { formatMoney } from '../../shared/presentation/money';
import { ChipTone } from '../../shared/ui/status-chip/status-chip';
import { IconName } from '../../shared/ui/icon/icon';
import { NeedsActionGroup, NeedsActionGroupKey, NeedsActionRow } from './needs-action.models';

/** Each group's heading, in the person's words. */
export const GROUP_HEADINGS: Readonly<Record<NeedsActionGroupKey, string>> = {
  'purchase-orders-to-approve': 'Purchase orders waiting for your approval',
  'change-orders-to-approve': 'Change orders waiting for your approval',
  'bills-to-issue': 'Bills ready to issue',
  'claims-to-certify': 'Subcontract claims to certify',
  'payroll-to-approve': 'Payroll waiting for approval',
  'bills-to-chase': 'Overdue bills to chase',
  'deliveries-expected': 'Deliveries still to come',
  'bonds-to-attend': 'Bonds that have expired or expire soon',
  'progress-not-updated': 'Progress not updated for 14 days',
  'attendance-not-recorded': 'Attendance not recorded yesterday',
  'statutory-needs-setup': 'Contribution and tax tables need setup',
};

/** What the person can do, as far as the links go: it decides which page a delivery opens and whether "See all" exists. */
export interface LinkContext {
  readonly can: (permission: string) => boolean;
}

export type Link = readonly (string | number)[];

/** The page where the record is decided: the group and the ids the server sent decide it, never a route from the server. */
export function rowLink(key: NeedsActionGroupKey, row: NeedsActionRow, context: LinkContext): Link {
  switch (key) {
    case 'purchase-orders-to-approve':
      return ['/purchasing/orders', row.id];
    case 'change-orders-to-approve':
      return ['/projects', row.projectId ?? '', 'changes'];
    case 'bills-to-issue':
    case 'bills-to-chase':
      return ['/billing/bills', row.id];
    case 'claims-to-certify':
      return ['/subcontracts', row.parentId ?? row.id];
    case 'payroll-to-approve':
      return ['/payroll', row.id];
    case 'deliveries-expected':
      // Someone who may not read purchase orders (a Warehouse Keeper) records it on the quantity-only receiving page.
      return context.can(Permission.purchasingRead)
        ? ['/purchasing/orders', row.id]
        : ['/receiving', row.id];
    case 'bonds-to-attend':
      return ['/bonds', row.id];
    case 'progress-not-updated':
      return ['/projects', row.id, 'progress'];
    case 'attendance-not-recorded':
      return ['/projects', row.id, 'team'];
    case 'statutory-needs-setup':
      return ['/payroll/statutory'];
  }
}

export interface SeeAll {
  readonly link: Link;
  readonly queryParams: Params;
}

/** The nearest existing list that shows exactly the same records, or null when there is none (the page then says "N in total"). */
export function seeAllLink(key: NeedsActionGroupKey, context: LinkContext): SeeAll | null {
  switch (key) {
    case 'purchase-orders-to-approve':
      return context.can(Permission.purchasingRead)
        ? { link: ['/purchasing'], queryParams: { status: 'Draft' } }
        : null;
    case 'deliveries-expected':
      return context.can(Permission.receivingRecord)
        ? { link: ['/receiving'], queryParams: {} }
        : null;
    default:
      return null;
  }
}

export interface UrgentChip {
  readonly label: string;
  readonly tone: ChipTone;
  readonly icon: IconName;
}

/** An urgent row says so in words and with an icon, never colour alone. */
export function urgentChip(key: NeedsActionGroupKey): UrgentChip {
  return key === 'bonds-to-attend'
    ? { label: 'Expired', tone: 'danger', icon: 'warning' }
    : { label: 'Overdue', tone: 'danger', icon: 'clock' };
}

export interface PresentedRow {
  readonly key: string;
  readonly label: string;
  readonly project: string;
  readonly detail: string;
  readonly amount: string;
  readonly urgent: UrgentChip | null;
  readonly link: Link;
  readonly openLabel: string;
  readonly primary: boolean;
}

export interface PresentedGroup {
  readonly key: NeedsActionGroupKey;
  readonly heading: string;
  readonly count: number;
  readonly rows: readonly PresentedRow[];
  /** "See all 12" to the matching list, or null. */
  readonly seeAll: (SeeAll & { readonly label: string }) | null;
  /** "12 in total" when more wait than are shown and no list matches; empty otherwise. */
  readonly totalNote: string;
}

/**
 * The page's groups as shown: groups with nothing are left out, every row gets its page and its amount in the company currency, and the
 * one primary action of the page is the first urgent row's Open (or the first row's, when nothing is urgent).
 */
export function presentGroups(
  groups: readonly NeedsActionGroup[],
  context: LinkContext & { readonly currencyCode: string },
): PresentedGroup[] {
  const shown = groups.filter((group) => group.count > 0 && group.rows.length > 0);
  const all = shown.flatMap((group) => group.rows);
  const primaryRow = all.find((row) => row.isUrgent) ?? all[0];

  return shown.map((group) => {
    const more = group.count > group.rows.length;
    const list = more ? seeAllLink(group.key, context) : null;
    return {
      key: group.key,
      heading: GROUP_HEADINGS[group.key],
      count: group.count,
      rows: group.rows.map((row, index) => ({
        key: `${group.key}-${row.id}-${index}`,
        label: row.label,
        project: row.projectName && row.projectName !== row.label ? row.projectName : '',
        detail: row.detail,
        amount:
          row.amountMinor === undefined || row.amountMinor === null
            ? ''
            : formatMoney(row.amountMinor, context.currencyCode),
        urgent: row.isUrgent ? urgentChip(group.key) : null,
        link: rowLink(group.key, row, context),
        openLabel: `Open ${row.label}`,
        primary: row === primaryRow,
      })),
      seeAll: list ? { ...list, label: `See all ${group.count}` } : null,
      totalNote: more && !list ? `${group.count} in total` : '',
    };
  });
}

/** The sum of the groups' counts: the badge's number. */
export function totalOf(groups: readonly NeedsActionGroup[]): number {
  return groups.reduce((sum, group) => sum + group.count, 0);
}
