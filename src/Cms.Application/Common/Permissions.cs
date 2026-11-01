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

    // Commercial control: suppliers and purchasing, progress and earned value, change orders, client billing, subcontracts, statutory deductions.
    public const string SuppliersRead = "suppliers.read";
    public const string SuppliersManage = "suppliers.manage";
    public const string PurchasingRead = "purchasing.read";
    public const string PurchasingManage = "purchasing.manage";
    public const string PurchasingApprove = "purchasing.approve";
    public const string ProgressRead = "progress.read";
    public const string ProgressWrite = "progress.write";
    public const string ChangeOrdersRead = "changeorders.read";
    public const string ChangeOrdersManage = "changeorders.manage";
    public const string ChangeOrdersApprove = "changeorders.approve";
    public const string BillingRead = "billing.read";
    public const string BillingManage = "billing.manage";
    public const string BillingApprove = "billing.approve";
    public const string SubcontractsRead = "subcontracts.read";
    public const string SubcontractsManage = "subcontracts.manage";
    public const string SubcontractsApprove = "subcontracts.approve";
    public const string StatutoryManage = "statutory.manage";

    // Philippine practice: delivery receipts, the bonds register, premium pay rules and the holiday calendar.
    public const string PurchasingReceive = "purchasing.receive";
    public const string BondsRead = "bonds.read";
    public const string BondsManage = "bonds.manage";
    public const string PayrollRulesManage = "payrollrules.manage";

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
        [AuditRead] = Admin | Acct,

        // Commercial control. Money screens follow the people who see costs; whoever drafts is never the only one who can commit money.
        // A warehouse keeper needs to name a supplier when registering stock, so supplier names are theirs to read; amounts are not.
        [SuppliersRead] = Admin | Pm | Cost | Acct | Wh | View,
        [SuppliersManage] = Admin | Cost | Acct,
        [PurchasingRead] = Admin | Pm | Cost | Acct | View,
        [PurchasingManage] = Admin | Cost | Acct,
        [PurchasingApprove] = Admin | Pm | Acct,
        [ProgressRead] = Admin | Pm | Cost | Acct | View,
        [ProgressWrite] = Admin | Pm | Cost,
        [ChangeOrdersRead] = Admin | Pm | Cost | Acct | View,
        [ChangeOrdersManage] = Admin | Pm | Cost,
        // Approving a change order, and freezing the budget baseline, change what the company expects to earn and spend.
        [ChangeOrdersApprove] = Admin | Pm,
        [BillingRead] = Admin | Pm | Cost | Acct | View,
        [BillingManage] = Admin | Pm | Acct,
        [BillingApprove] = Admin | Acct,
        [SubcontractsRead] = Admin | Pm | Cost | Acct | View,
        [SubcontractsManage] = Admin | Pm | Cost,
        [SubcontractsApprove] = Admin | Acct,
        // The statutory contribution and tax tables change by law; the accountant owns them. HR reads them through payroll.read.
        [StatutoryManage] = Admin | Acct,

        // Receiving a delivery says what physically arrived; the people who can read purchase orders and raise them may record it.
        [PurchasingReceive] = Admin | Pm | Cost | Acct,
        [BondsRead] = Admin | Pm | Cost | Acct | View,
        [BondsManage] = Admin | Pm | Acct,
        // Premium factors and holidays change by law and proclamation; the people who run payroll keep them, every change is audited.
        [PayrollRulesManage] = Admin | Acct | Hr
    };

    public static IReadOnlyCollection<string> All => Matrix.Keys;

    public static AccessRole RolesFor(string permission) =>
        Matrix.TryGetValue(permission, out var roles) ? roles : throw new ArgumentException($"Unknown permission '{permission}'.", nameof(permission));

    public static bool IsAllowed(AccessRole held, string permission) => held.HasAny(RolesFor(permission));
}
