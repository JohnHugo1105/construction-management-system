namespace ConstructOPX.Domain.Features.Identity;

/// <summary>
/// What a person may work on. Roles combine (a person can be both Cost Engineer and Accountant).
/// The retired desktop app mapped four module letters (c, a, p, h) to access and treated an empty selection as full access;
/// here an account with no role can do nothing, so a mistake fails closed.
/// </summary>
[Flags]
public enum AccessRole
{
    None = 0,
    Administrator = 1,
    ProjectManager = 2,
    CostEngineer = 4,
    Accountant = 8,
    WarehouseKeeper = 16,
    HrOfficer = 32,
    Viewer = 64
}

public static class AccessRoles
{
    public const AccessRole All = AccessRole.Administrator | AccessRole.ProjectManager | AccessRole.CostEngineer |
        AccessRole.Accountant | AccessRole.WarehouseKeeper | AccessRole.HrOfficer | AccessRole.Viewer;

    public static bool HasAny(this AccessRole held, AccessRole needed) => (held & needed) != AccessRole.None;

    public static bool IsValid(AccessRole roles) => roles != AccessRole.None && (roles & ~All) == AccessRole.None;

    public static IReadOnlyList<AccessRole> Split(AccessRole roles) =>
        Enum.GetValues<AccessRole>().Where(role => role != AccessRole.None && (roles & role) == role).ToArray();

    public static string Label(AccessRole role) => role switch
    {
        AccessRole.Administrator => "Administrator",
        AccessRole.ProjectManager => "Project Manager",
        AccessRole.CostEngineer => "Cost Engineer",
        AccessRole.Accountant => "Accountant",
        AccessRole.WarehouseKeeper => "Warehouse Keeper",
        AccessRole.HrOfficer => "HR Officer",
        AccessRole.Viewer => "Viewer",
        _ => role.ToString()
    };
}
