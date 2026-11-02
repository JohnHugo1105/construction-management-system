using ConstructOPX.Application.Common;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.Domain.Features.Inventory;
using ConstructOPX.Domain.Features.Organizations;
using ConstructOPX.Domain.Features.Payroll;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.Workforce;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConstructOPX.Infrastructure.Seeding;

/// <summary>
/// Fills an EMPTY development database with a small, realistic company so every screen has something to show: one user per role, three
/// projects (one archived), a budget with every health state, costs, a crew with attendance ready for a payroll run, and stock with a
/// charged transfer. It builds everything through the domain methods, so the rules are exercised and the figures are always valid.
/// It runs only in Development, only when switched on (<c>Seeding:Enabled</c>), and never touches a database that already has users.
/// </summary>
public sealed class DevelopmentSeeder(ConstructOpxDbContext db, IPasswordHasher hasher, IClock clock, ILogger<DevelopmentSeeder> logger)
{
    /// <summary>The password of every demo account. It is public on purpose: this data exists only in development.</summary>
    public const string DemoPassword = "Demo-passw0rd-2026";

    private static readonly (string Username, string Name, AccessRole Roles)[] Accounts =
    [
        ("admin", "Ana Administrator", AccessRole.Administrator),
        ("manager", "Paolo Project-Manager", AccessRole.ProjectManager),
        ("cost", "Carla Cost-Engineer", AccessRole.CostEngineer),
        ("accounts", "Alma Accountant", AccessRole.Accountant),
        ("hrofficer", "Hannah HR-Officer", AccessRole.HrOfficer),
        ("store", "Wilson Warehouse-Keeper", AccessRole.WarehouseKeeper),
        ("viewer", "Vince Viewer", AccessRole.Viewer)
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await db.Set<User>().IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            logger.LogInformation("Seeding skipped: the database already has users.");
            return;
        }

        var now = clock.UtcNow;
        var organization = Organization.Create(Guid.NewGuid(), "Demo Builders Inc.", now);
        var tenant = organization.Id;
        var today = organization.TodayLocal(now);
        db.Add(organization);

        var users = Accounts.ToDictionary(account => account.Username,
            account => User.Create(Guid.NewGuid(), tenant, account.Username, account.Name, $"{account.Username}@example.test",
                hasher.Hash(DemoPassword), account.Roles, mustChangePassword: false, now));
        db.AddRange(users.Values);
        var by = users["admin"].Id;

        // ---- Projects
        var riverside = Project.Create(Guid.NewGuid(), tenant, "Riverside Residences", "Riverside Land Corp.", "Cebu City", 48_500_000_00,
            today.AddMonths(-6), today.AddMonths(8), now);
        riverside.Start(now);
        var towerA = riverside.AddSubProject(Guid.NewGuid(), "Tower A", today.AddMonths(-6), now);
        riverside.AddSubProject(Guid.NewGuid(), "Clubhouse", today.AddMonths(-2), now);
        var harbor = Project.Create(Guid.NewGuid(), tenant, "Harbor Warehouse", "Harbor Logistics", "Mandaue City", 12_000_000_00,
            today.AddDays(-10), today.AddMonths(10), now);
        var annex = Project.Create(Guid.NewGuid(), tenant, "Old School Annex", "Dept. of Education", "Talisay City", 6_250_000_00,
            today.AddYears(-1), today.AddMonths(-3), now);
        annex.Start(now);
        annex.Complete(now);
        annex.Archive(ArchiveReason.Finished, null, now);
        db.AddRange(riverside, harbor, annex);

        // ---- Budget (Riverside) chosen so the dashboard shows every health state
        void Budget(string wbs, string description, ItemType type, decimal quantity, string unit, long unitCostMinor, VatTreatment vat = VatTreatment.Inclusive) =>
            db.Add(BudgetLine.Create(Guid.NewGuid(), tenant, riverside, null, null, null, null, wbs, description, type, quantity, unit, unitCostMinor, vat,
                organization.VatRateBps, today.AddMonths(-6), null, by, null, now));

        Budget("3300", "Ready-mix concrete 4000 psi", ItemType.Material, 600m, "m³", 5_800_00);          // 3,480,000.00
        Budget("3200", "Deformed bars, grade 60", ItemType.Material, 42_000m, "kg", 52_00);               // 2,184,000.00
        Budget("4200", "Hollow blocks 6 inch", ItemType.Material, 60_000m, "pcs", 14_00);                 //   840,000.00
        Budget("9300", "Floor tiles 60x60", ItemType.Material, 5_200m, "m²", 780_00);                     // 4,056,000.00
        Budget("15400", "Plumbing fixtures", ItemType.Material, 1m, "lot", 1_850_000_00);                 // 1,850,000.00
        Budget("3100", "Formwork crew", ItemType.Labor, 1m, "l.s", 2_400_000_00, VatTreatment.Exempt);    // 2,400,000.00
        Budget("16200", "Electrical installation", ItemType.Subcontract, 1m, "l.s", 3_100_000_00);        // 3,100,000.00

        // ---- Actual costs: concrete over budget, rebar near its limit, tiles on track, plumbing unbudgeted extra
        void Cost(string wbs, string description, ItemType type, decimal quantity, string unit, long unitCostMinor, string dealer, string invoice, int daysAgo) =>
            db.Add(CostEntry.RecordManual(Guid.NewGuid(), tenant, riverside, null, null, null, null, wbs, description, type, quantity, unit, unitCostMinor,
                VatTreatment.Inclusive, organization.VatRateBps, today.AddDays(-daysAgo), null, dealer, invoice, users["cost"].Id, null, today, now));

        Cost("3300", "Ready-mix concrete 4000 psi", ItemType.Material, 400m, "m³", 5_900_00, "Metro Concrete Batching", "MCB-10231", 120);
        Cost("3300", "Ready-mix concrete 4000 psi", ItemType.Material, 215m, "m³", 6_050_00, "Metro Concrete Batching", "MCB-10688", 45);
        Cost("3200", "Deformed bars, grade 60", ItemType.Material, 31_500m, "kg", 53_50, "Visayas Steel Trading", "VST-5521", 100);
        Cost("3200", "Deformed bars, grade 60", ItemType.Material, 8_000m, "kg", 54_00, "Visayas Steel Trading", "VST-5702", 30);
        Cost("4200", "Hollow blocks 6 inch", ItemType.Material, 24_000m, "pcs", 14_50, "Cebu Block Makers", "CBM-0912", 60);
        Cost("9300", "Floor tiles 60x60", ItemType.Material, 1_100m, "m²", 790_00, "Tilemart Cebu", "TM-30991", 15);
        Cost("15200", "Pipe rental and fittings (not budgeted)", ItemType.Equipment, 1m, "lot", 215_000_00, "Cebu Plumbing Supply", "CPS-7781", 9);

        // ---- Workforce: a crew assigned to Riverside with two weeks of attendance, ready for a payroll run
        var crew = new[]
        {
            ("W-001", "Juan", "Dela Cruz", "Foreman", 1_100_00L),
            ("W-002", "Pedro", "Reyes", "Mason", 800_00L),
            ("W-003", "Maria", "Santos", "Steelman", 850_00L),
            ("W-004", "Jose", "Garcia", "Carpenter", 780_00L),
            ("W-005", "Ana", "Bautista", "Helper", 610_00L),
            ("W-006", "Rico", "Mendoza", "Equipment operator", 950_00L)
        }.Select(person => Worker.Hire(Guid.NewGuid(), tenant, person.Item1, person.Item2, null, person.Item3, today.AddMonths(-5), person.Item4,
            person.Item5, RateUnit.Daily, users["hrofficer"].Id, today, now)).ToList();
        var foreman = crew[0];
        foreman.ChangeRate(RateChangeCategory.RateAdjustment, "Foreman", 1_150_00, RateUnit.Daily, today.AddDays(-7), "Annual review", users["hrofficer"].Id, now);
        foreach (var worker in crew) worker.AssignTo(riverside, worker == foreman ? towerA.Id : null, today.AddMonths(-4), users["hrofficer"].Id, now);
        // The crew rests on Sundays, which makes a Sunday shift a rest-day shift.
        foreach (var worker in crew) worker.SetRestDay(DayOfWeek.Sunday, now);
        db.AddRange(crew);

        // Two illustrative holidays (invented for the demo, not real proclamations): a regular holiday on a weekday inside the attendance below,
        // so the crew's pay shows the holiday premium, and a special day a few days ahead.
        var holiday = today.AddDays(-9);
        while (holiday.DayOfWeek == DayOfWeek.Sunday) holiday = holiday.AddDays(-1);
        db.Add(CompanyHoliday.Create(Guid.NewGuid(), tenant, holiday, "Demo regular holiday", HolidayType.Regular, now));
        db.Add(CompanyHoliday.Create(Guid.NewGuid(), tenant, today.AddDays(5), "Demo special non-working day", HolidayType.Special, now));

        for (var daysAgo = 13; daysAgo >= 1; daysAgo--)
        {
            var date = today.AddDays(-daysAgo);
            if (date.DayOfWeek == DayOfWeek.Sunday) continue;
            foreach (var worker in crew)
                db.Add(AttendanceEntry.Record(Guid.NewGuid(), tenant, worker, riverside, worker == foreman ? towerA.Id : null, date, 8m,
                    date.DayOfWeek == DayOfWeek.Saturday || worker.EmployeeNumber == "W-002" && daysAgo % 3 == 0 ? 2m : 0m, null,
                    organization.HoursPerDay, users["manager"].Id, null, today, now));
        }

        // ---- Inventory: tools in the warehouse, a generator and an excavator, one charged transfer to the project
        var hammers = InventoryItem.Register(Guid.NewGuid(), tenant, "Claw hammer 16 oz", InventoryCategory.Tool, "Stanley", null, "pcs", "Cebu Hardware", "CH-221",
            today.AddMonths(-5), 450_00, 24m, null, users["store"].Id, null, today, now);
        var generator = InventoryItem.Register(Guid.NewGuid(), tenant, "Diesel generator 20 kVA", InventoryCategory.Equipment, "Cummins", "CUM-20K-77812", "unit",
            "Power Equipment Co.", "PEC-1020", today.AddMonths(-4), 640_000_00, 1m, null, users["store"].Id, null, today, now);
        var excavator = InventoryItem.Register(Guid.NewGuid(), tenant, "Excavator PC200", InventoryCategory.Machinery, "Komatsu", "PC200-8-A41127", "unit",
            "Heavy Machinery Sales", "HMS-5509", today.AddMonths(-5), 4_850_000_00, 1m, null, users["store"].Id, null, today, now);
        var site = StockLocation.Project(riverside.Id);
        hammers.Transfer(StockLocation.Warehouse, site, 10m, today.AddMonths(-3), false, "For the formwork crew", users["store"].Id, null, today, now);
        generator.Transfer(StockLocation.Warehouse, site, 1m, today.AddMonths(-3), false, null, users["store"].Id, null, today, now);
        excavator.Transfer(StockLocation.Warehouse, site, 1m, today.AddMonths(-2), false, null, users["store"].Id, null, today, now);
        db.AddRange(hammers, generator, excavator);

        // The statutory tables are added to this same save; every other layer below saves itself, in the order that lets each find what the
        // one before it made (firms and orders, subcontracts, a locked budget with change orders, progress, then billing against the price).
        await StatutoryDemo.SeedAsync(db, tenant, today, now, cancellationToken);
        db.Add(Domain.Features.Audit.AuditEntry.Record(tenant, by, "admin", "System.Seeded", nameof(Organization), tenant,
            "Demo data was created for development.", null, now));
        await db.SaveChangesAsync(cancellationToken);

        await PurchasingDemo.SeedAsync(db, organization, riverside, users["cost"].Id, users["accounts"].Id, today, now, cancellationToken);
        await SubcontractDemo.SeedAsync(db, organization, riverside, users["manager"].Id, users["accounts"].Id, today, now, cancellationToken);
        await ChangeOrderDemo.SeedAsync(db, tenant, riverside.Id, users["manager"].Id, users["cost"].Id, organization.VatRateBps, today, now, cancellationToken);
        await ProgressDemo.SeedAsync(db, tenant, today, now, cancellationToken);
        await BillingDemo.SeedAsync(db, organization, riverside, harbor, users["accounts"].Id, users["manager"].Id, today, now, cancellationToken);
        await BondDemo.SeedAsync(db, organization, riverside, users["accounts"].Id, today, now, cancellationToken);
        await SiteDiaryDemo.SeedAsync(db, tenant, riverside, users["manager"].Id, today, now, cancellationToken);
        await TimeClockDemo.SeedAsync(db, organization, riverside, crew, users["hrofficer"].Id, today, now, cancellationToken);
        logger.LogInformation("Demo data created for {Company}. Sign in as admin, manager, cost, accounts, hrofficer, store or viewer with the demo password.", organization.Name);
    }
}
