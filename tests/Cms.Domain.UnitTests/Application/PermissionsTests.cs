using Cms.Application.Common;
using Cms.Domain.Features.Identity;

namespace Cms.Domain.UnitTests.Application;

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
    public void An_unknown_permission_is_an_error_not_a_silent_denial() =>
        Assert.Throws<ArgumentException>(() => Permissions.IsAllowed(AccessRole.Administrator, "projects.delete-everything"));
}
