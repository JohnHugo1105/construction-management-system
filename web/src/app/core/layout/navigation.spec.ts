import { Permission } from '../session/permissions';
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
        Permission.usersManage,
      ),
    );

    expect(labels(items)).toEqual([
      'Dashboard',
      'Projects',
      'Workers',
      'Payroll',
      'Stock',
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

    expect(labels(primary)).toEqual(['Dashboard', 'Projects', 'Workers', 'Payroll']);
    expect(labels(overflow)).toEqual(['Stock', 'Purchasing', 'Subcontracts', 'Billing', 'Admin']);
    expect(primary.length + 1).toBeLessThanOrEqual(5);
  });
});
