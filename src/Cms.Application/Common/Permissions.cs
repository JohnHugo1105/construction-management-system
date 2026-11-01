using Cms.Domain.Features.Identity;

namespace Cms.Application.Common;

/// <summary>
/// Who may do what. Endpoints enforce these through authorization policies; the matrix lives here, next to the roles, so it is
/// documented and tested in one place (see docs/business-rules.md, "Access").
/// </summary>
public static class Permissions
{
    public const string ProjectsRead = "projects.read";
    public const string ProjectsManage = "projects.manage";
    public const string CostsRead = "costs.read";
    public const string CostsWrite = "costs.write";
    public const string WorkforceRead = "workforce.read";
    public const string WorkforceManage = "workforce.manage";
    public const string AttendanceWrite = "attendance.write";
    public const string PayrollRead = "payroll.read";
    public const string PayrollRun = "payroll.run";
    public const string PayrollApprove = "payroll.approve";
    public const string InventoryRead = "inventory.read";
    public const string InventoryManage = "inventory.manage";
    public const string UsersManage = "users.manage";
    public const string SettingsManage = "settings.manage";
    public const string AuditRead = "audit.read";

    private const AccessRole Admin = AccessRole.Administrator;
    private const AccessRole Pm = AccessRole.ProjectManager;
    private const AccessRole Cost = AccessRole.CostEngineer;
    private const AccessRole Acct = AccessRole.Accountant;
    private const AccessRole Wh = AccessRole.WarehouseKeeper;
    private const AccessRole Hr = AccessRole.HrOfficer;
    private const AccessRole View = AccessRole.Viewer;

    private static readonly Dictionary<string, AccessRole> Matrix = new(StringComparer.Ordinal)
    {
        // Everyone who signs in with a role can see the project list; only managers change it.
        [ProjectsRead] = AccessRoles.All,
        [ProjectsManage] = Admin | Pm,
        [CostsRead] = Admin | Pm | Cost | Acct | View,
        [CostsWrite] = Admin | Cost | Acct,
        [WorkforceRead] = Admin | Pm | Hr | Acct | View,
        [WorkforceManage] = Admin | Hr,
        // Site-level supervisors (project managers) record attendance; HR records it for the office.
        [AttendanceWrite] = Admin | Pm | Hr,
        [PayrollRead] = Admin | Hr | Acct,
        [PayrollRun] = Admin | Hr | Acct,
        [PayrollApprove] = Admin | Acct,
        [InventoryRead] = Admin | Pm | Wh | Cost | View,
        [InventoryManage] = Admin | Wh,
        [UsersManage] = Admin,
        [SettingsManage] = Admin,
        [AuditRead] = Admin | Acct
    };

    public static IReadOnlyCollection<string> All => Matrix.Keys;

    public static AccessRole RolesFor(string permission) =>
        Matrix.TryGetValue(permission, out var roles) ? roles : throw new ArgumentException($"Unknown permission '{permission}'.", nameof(permission));

    public static bool IsAllowed(AccessRole held, string permission) => held.HasAny(RolesFor(permission));
}
