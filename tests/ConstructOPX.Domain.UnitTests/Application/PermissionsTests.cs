using ConstructOPX.Application.Common;
using ConstructOPX.Domain.Features.Identity;

namespace ConstructOPX.Domain.UnitTests.Application;

public sealed class PermissionsTests
{
    [Fact]
    public void Administrators_hold_every_permission() =>
        Assert.All(Permissions.All, permission => Assert.True(Permissions.IsAllowed(AccessRole.Administrator, permission), permission));

    [Fact]
    public void Viewers_can_read_but_never_write()
    {
        foreach (var write in new[] { Permissions.ProjectsManage, Permissions.CostsWrite, Permissions.WorkforceManage, Permissions.AttendanceWrite,
                     Permissions.PayrollRun, Permissions.PayrollApprove, Permissions.InventoryManage, Permissions.UsersManage, Permissions.SettingsManage })
            Assert.False(Permissions.IsAllowed(AccessRole.Viewer, write), write);
        Assert.True(Permissions.IsAllowed(AccessRole.Viewer, Permissions.ProjectsRead));
        Assert.True(Permissions.IsAllowed(AccessRole.Viewer, Permissions.CostsRead));
    }

    [Fact]
    public void An_account_with_no_role_holds_no_permission() =>
        Assert.All(Permissions.All, permission => Assert.False(Permissions.IsAllowed(AccessRole.None, permission), permission));

    [Fact]
    public void Pay_is_visible_only_to_those_who_run_it_and_approval_is_narrower_than_running_it()
    {
        Assert.False(Permissions.IsAllowed(AccessRole.ProjectManager, Permissions.PayrollRead));
        Assert.False(Permissions.IsAllowed(AccessRole.CostEngineer, Permissions.PayrollRead));
        Assert.False(Permissions.IsAllowed(AccessRole.WarehouseKeeper, Permissions.PayrollRead));
        Assert.True(Permissions.IsAllowed(AccessRole.HrOfficer, Permissions.PayrollRun));
        Assert.False(Permissions.IsAllowed(AccessRole.HrOfficer, Permissions.PayrollApprove));
        Assert.True(Permissions.IsAllowed(AccessRole.Accountant, Permissions.PayrollApprove));
    }

    [Fact]
    public void Roles_combine_so_a_person_can_hold_several()
    {
        var both = AccessRole.CostEngineer | AccessRole.HrOfficer;
        Assert.True(Permissions.IsAllowed(both, Permissions.CostsWrite));
        Assert.True(Permissions.IsAllowed(both, Permissions.WorkforceManage));
        Assert.False(Permissions.IsAllowed(both, Permissions.PayrollApprove));
    }

    [Fact]
    public void Commercial_control_never_lets_one_role_do_every_step_alone_below_the_administrator()
    {
        // Drafting is wider than approving, for each of the four money workflows.
        foreach (var (manage, approve) in new[]
                 {
                     (Permissions.PurchasingManage, Permissions.PurchasingApprove), (Permissions.ChangeOrdersManage, Permissions.ChangeOrdersApprove),
                     (Permissions.BillingManage, Permissions.BillingApprove), (Permissions.SubcontractsManage, Permissions.SubcontractsApprove)
                 })
            Assert.NotEqual(Permissions.RolesFor(manage), Permissions.RolesFor(approve));

        // Viewers read the money screens and change nothing; a warehouse keeper sees supplier names but no amounts.
        foreach (var write in new[] { Permissions.SuppliersManage, Permissions.PurchasingManage, Permissions.PurchasingApprove, Permissions.ProgressWrite,
                     Permissions.ChangeOrdersManage, Permissions.ChangeOrdersApprove, Permissions.BillingManage, Permissions.BillingApprove,
                     Permissions.SubcontractsManage, Permissions.SubcontractsApprove, Permissions.StatutoryManage })
            Assert.False(Permissions.IsAllowed(AccessRole.Viewer, write), write);
        Assert.True(Permissions.IsAllowed(AccessRole.WarehouseKeeper, Permissions.SuppliersRead));
        foreach (var money in new[] { Permissions.PurchasingRead, Permissions.BillingRead, Permissions.SubcontractsRead, Permissions.ChangeOrdersRead, Permissions.ProgressRead })
            Assert.False(Permissions.IsAllowed(AccessRole.WarehouseKeeper, money), money);

        // Only owners of the project decide a change order; only the accounts side certifies and bills.
        Assert.False(Permissions.IsAllowed(AccessRole.CostEngineer, Permissions.ChangeOrdersApprove));
        Assert.True(Permissions.IsAllowed(AccessRole.ProjectManager, Permissions.ChangeOrdersApprove));
        Assert.False(Permissions.IsAllowed(AccessRole.ProjectManager, Permissions.BillingApprove));
        Assert.True(Permissions.IsAllowed(AccessRole.Accountant, Permissions.SubcontractsApprove));
        Assert.False(Permissions.IsAllowed(AccessRole.HrOfficer, Permissions.StatutoryManage));
    }

    [Fact]
    public void Screens_that_show_the_contract_price_or_amounts_derived_from_it_stay_within_the_people_who_see_costs()
    {
        // The change-order limit, the down payment still to bill, purchase order totals and bond amounts can all be worked back to costs or
        // the contract price. Widening one of these permissions beyond costs.read would leak them, so the test fails first.
        var costs = Permissions.RolesFor(Permissions.CostsRead);
        foreach (var permission in new[] { Permissions.ChangeOrdersRead, Permissions.BillingRead, Permissions.BondsRead, Permissions.PurchasingRead,
                     Permissions.SubcontractsRead, Permissions.ProgressRead, Permissions.ScheduleRead })
            Assert.Equal(default, Permissions.RolesFor(permission) & ~costs);
    }

    [Fact]
    public void Receiving_is_for_administrators_and_warehouse_keepers_and_gives_no_purchasing_access()
    {
        Assert.True(Permissions.IsAllowed(AccessRole.Administrator, Permissions.ReceivingRecord));
        Assert.True(Permissions.IsAllowed(AccessRole.WarehouseKeeper, Permissions.ReceivingRecord));
        foreach (var role in new[] { AccessRole.ProjectManager, AccessRole.CostEngineer, AccessRole.Accountant, AccessRole.HrOfficer, AccessRole.Viewer, AccessRole.None })
            Assert.False(Permissions.IsAllowed(role, Permissions.ReceivingRecord), role.ToString());

        // The keeper records deliveries on the quantity-only screen; the screens that carry prices stay closed to them.
        foreach (var money in new[] { Permissions.PurchasingRead, Permissions.PurchasingReceive, Permissions.PurchasingManage, Permissions.PurchasingApprove })
            Assert.False(Permissions.IsAllowed(AccessRole.WarehouseKeeper, money), money);
    }

    [Fact]
    public void Everyone_reads_their_own_needs_action_queue()
    {
        Assert.Equal(AccessRoles.All, Permissions.RolesFor(Permissions.ActionsRead));
        foreach (var role in AccessRoles.Split(AccessRoles.All))
            Assert.True(Permissions.IsAllowed(role, Permissions.ActionsRead), role.ToString());
        Assert.False(Permissions.IsAllowed(AccessRole.None, Permissions.ActionsRead));
    }

    [Fact]
    public void An_unknown_permission_is_an_error_not_a_silent_denial() =>
        Assert.Throws<ArgumentException>(() => Permissions.IsAllowed(AccessRole.Administrator, "projects.delete-everything"));
}
