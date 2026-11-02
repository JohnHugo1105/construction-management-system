import { NEEDS_ACTION_PERMISSIONS, Permission } from '../session/permissions';
import { IconName } from '../../shared/ui/icon/icon';

export interface NavItem {
  readonly label: string;
  readonly link: string;
  readonly icon: IconName;
  /** The entry is shown to anyone holding at least one of these. */
  readonly permissions: readonly string[];
  /** The entry carries the "Needs your action" badge (how many things wait on the person). */
  readonly showsWaitingCount?: boolean;
}

/**
 * The main navigation, in order. An entry is hidden (not greyed out) from anyone who cannot use it. To add a section, add a
 * line here and a lazy route in app.routes.ts.
 */
export const NAV_ITEMS: readonly NavItem[] = [
  {
    label: 'Dashboard',
    link: '/dashboard',
    icon: 'dashboard',
    permissions: [Permission.costsRead],
  },
  {
    label: 'Needs action',
    link: '/needs-action',
    icon: 'check-circle',
    // Only people whose role acts on something see it, so nobody is sent to a page that can never fill.
    permissions: NEEDS_ACTION_PERMISSIONS,
    showsWaitingCount: true,
  },
  { label: 'Tasks', link: '/tasks', icon: 'check', permissions: [Permission.tasksUse] },
  {
    label: 'Projects',
    link: '/projects',
    icon: 'building',
    permissions: [Permission.projectsRead],
  },
  { label: 'Workers', link: '/workers', icon: 'users', permissions: [Permission.workforceRead] },
  { label: 'Payroll', link: '/payroll', icon: 'payroll', permissions: [Permission.payrollRead] },
  { label: 'Stock', link: '/stock', icon: 'stock', permissions: [Permission.inventoryRead] },
  {
    label: 'Receiving',
    link: '/receiving',
    icon: 'stock',
    permissions: [Permission.receivingRecord],
  },
  {
    label: 'Purchasing',
    link: '/purchasing',
    icon: 'cart',
    permissions: [Permission.purchasingRead],
  },
  {
    label: 'Subcontracts',
    link: '/subcontracts',
    icon: 'contract',
    permissions: [Permission.subcontractsRead],
  },
  { label: 'Billing', link: '/billing', icon: 'receipt', permissions: [Permission.billingRead] },
  {
    label: 'Admin',
    link: '/admin',
    icon: 'shield',
    permissions: [Permission.usersManage, Permission.settingsManage, Permission.auditRead],
  },
];

/** The entries a person may use. */
export function visibleNavItems(
  items: readonly NavItem[],
  hasAnyPermission: (...permissions: readonly string[]) => boolean,
): NavItem[] {
  return items.filter((item) => hasAnyPermission(...item.permissions));
}

export interface BottomBarItems {
  readonly primary: readonly NavItem[];
  readonly overflow: readonly NavItem[];
}

/**
 * The bottom bar on a phone holds at most five entries. When there are more, the last slot becomes "More", which opens a sheet
 * with the rest, so every destination stays one tap away.
 */
export function splitForBottomBar(items: readonly NavItem[], maxSlots = 5): BottomBarItems {
  if (items.length <= maxSlots) return { primary: items, overflow: [] };
  return { primary: items.slice(0, maxSlots - 1), overflow: items.slice(maxSlots - 1) };
}
