using Cms.Domain.Features.Projects;
using Cms.Domain.Features.Workforce;

namespace Cms.Domain.UnitTests;

internal static class TestData
{
    public static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly DateTimeOffset Now = new(2026, 11, 2, 1, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly Today = new(2026, 11, 2);
    public static readonly DateOnly HireDate = new(2026, 1, 5);

    public static Project NewProject(string name = "Tower A", long? contractPriceMinor = 500_000_00) =>
        Project.Create(Guid.NewGuid(), Tenant, name, "ACME Realty", "Cebu City", contractPriceMinor, new DateOnly(2026, 1, 5), null, Now);

    /// <summary>A mason hired on 5 Jan 2026 at 800.00 a day.</summary>
    public static Worker NewWorker(string number = "W-001", long rateMinor = 80_000, RateUnit unit = RateUnit.Daily) =>
        Worker.Hire(Guid.NewGuid(), Tenant, number, "Juan", null, "Dela Cruz", HireDate, "Mason", rateMinor, unit, UserId, Today, Now);
}
