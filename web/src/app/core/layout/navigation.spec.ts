import { NEEDS_ACTION_PERMISSIONS, Permission } from '../session/permissions';
import { NAV_ITEMS, NavItem, splitForBottomBar, visibleNavItems } from './navigation';

function allowing(...held: string[]) {
  return (...needed: readonly string[]) => needed.some((permission) => held.includes(permission));
}

const labels = (items: readonly NavItem[]) => items.map((item) => item.label);

describe('navigation', () => {
  it('shows an administrator every section', () => {
    const items = visibleNavItems(
      NAV_ITEMS,
      allowing(
        Permission.costsRead,
        Permission.projectsRead,
        Permission.workforceRead,
        Permission.payrollRead,
        Permission.inventoryRead,
        Permission.receivingRecord,
        Permission.usersManage,
      ),
    );

    expect(labels(items)).toEqual([
      'Dashboard',
      'Needs action',
      'Projects',
      'Workers',
      'Payroll',
      'Stock',
      'Receiving',
      'Admin',
    ]);
  });

  it('hides what a person cannot use instead of greying it out', () => {
    const warehouse = visibleNavItems(
      NAV_ITEMS,
      allowing(Permission.projectsRead, Permission.inventoryRead),
    );

    expect(labels(warehouse)).toEqual(['Projects', 'Stock']);
  });

  it('shows a warehouse keeper Receiving, and shows it to nobody who lacks the permission to record deliveries', () => {
    const keeper = visibleNavItems(
      NAV_ITEMS,
      allowing(Permission.projectsRead, Permission.inventoryRead, Permission.receivingRecord),
    );
    const purchaser = visibleNavItems(
      NAV_ITEMS,
      allowing(Permission.projectsRead, Permission.purchasingRead, Permission.purchasingReceive),
    );

    expect(labels(keeper)).toEqual(['Needs action', 'Projects', 'Stock', 'Receiving']);
    expect(labels(purchaser)).toEqual(['Needs action', 'Projects', 'Purchasing']);
    expect(NAV_ITEMS.find((item) => item.label === 'Receiving')?.link).toBe('/receiving');
  });

  it('shows Admin to someone who can only read the activity log', () => {
    const accountant = visibleNavItems(
      NAV_ITEMS,
      allowing(Permission.projectsRead, Permission.auditRead),
    );

    expect(labels(accountant)).toEqual(['Projects', 'Admin']);
  });

  it('keeps every entry on the bottom bar while there are five or fewer', () => {
    const { primary, overflow } = splitForBottomBar(NAV_ITEMS.slice(0, 5));

    expect(primary).toHaveLength(5);
    expect(overflow).toHaveLength(0);
  });

  it('turns the fifth slot into "More" when there are more than five, so no destination is lost', () => {
    const { primary, overflow } = splitForBottomBar(NAV_ITEMS);

    expect(labels(primary)).toEqual(['Dashboard', 'Needs action', 'Tasks', 'Projects']);
    expect(labels(overflow)).toEqual([
      'Workers',
      'Payroll',
      'Stock',
      'Receiving',
      'Purchasing',
      'Subcontracts',
      'Billing',
      'Admin',
    ]);
    expect(primary.length + 1).toBeLessThanOrEqual(5);
  });

  it('puts Needs action right after the Dashboard, with the waiting badge', () => {
    const entry = NAV_ITEMS[1]!;

    expect(entry).toEqual(
      expect.objectContaining({
        label: 'Needs action',
        link: '/needs-action',
        showsWaitingCount: true,
      }),
    );
    expect(NAV_ITEMS.filter((item) => item.showsWaitingCount)).toHaveLength(1);
  });

  it('shows Needs action to anyone whose role acts on something it lists, one permission at a time', () => {
    for (const permission of NEEDS_ACTION_PERMISSIONS) {
      expect(
        labels(visibleNavItems(NAV_ITEMS, allowing(Permission.projectsRead, permission))),
      ).toContain('Needs action');
    }
    const hr = visibleNavItems(
      NAV_ITEMS,
      allowing(Permission.projectsRead, Permission.workforceRead, Permission.attendanceWrite),
    );
    expect(labels(hr)).toEqual(['Needs action', 'Projects', 'Workers']);
  });

  it('hides Needs action from a Viewer, whose page could never fill, although every role may read it', () => {
    const viewer = visibleNavItems(
      NAV_ITEMS,
      allowing(
        Permission.actionsRead,
        Permission.projectsRead,
        Permission.costsRead,
        Permission.purchasingRead,
        Permission.billingRead,
        Permission.bondsRead,
        Permission.progressRead,
      ),
    );

    expect(labels(viewer)).not.toContain('Needs action');
  });
});
