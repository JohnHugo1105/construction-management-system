using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Projects;
using Cms.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.IntegrationTests;

public sealed class PersistenceTests : IClassFixture<PersistenceFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 1, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 2);
    private readonly PersistenceFixture _fixture;

    public PersistenceTests(PersistenceFixture fixture) => _fixture = fixture;

    private static Project NewProject(Guid tenant, string name) =>
        Project.Create(Guid.NewGuid(), tenant, name, "ACME", "Cebu", 500_000_00, new DateOnly(2026, 1, 5), null, Now);

    [SqlServerFact]
    public async Task A_company_sees_only_its_own_rows()
    {
        var mine = NewProject(PersistenceFixture.TenantA, "Mine " + Guid.NewGuid());
        var theirs = NewProject(PersistenceFixture.TenantB, "Theirs " + Guid.NewGuid());
        await using (var seed = _fixture.Create(Guid.Empty)) { seed.AddRange(mine, theirs); await seed.SaveChangesAsync(); }

        await using var asA = _fixture.Create(PersistenceFixture.TenantA);
        await using var nobody = _fixture.Create(Guid.Empty);

        Assert.Contains(await asA.Set<Project>().Select(p => p.Id).ToListAsync(), id => id == mine.Id);
        Assert.DoesNotContain(await asA.Set<Project>().Select(p => p.Id).ToListAsync(), id => id == theirs.Id);
        Assert.Null(await asA.Set<Project>().SingleOrDefaultAsync(p => p.Id == theirs.Id));
        Assert.Empty(await nobody.Set<Project>().ToListAsync());
    }

    [SqlServerFact]
    public async Task Child_rows_are_filtered_by_company_too()
    {
        var project = NewProject(PersistenceFixture.TenantA, "With subs " + Guid.NewGuid());
        project.AddSubProject(Guid.NewGuid(), "Building 1", new DateOnly(2026, 2, 1), Now);
        await using (var seed = _fixture.Create(Guid.Empty)) { seed.Add(project); await seed.SaveChangesAsync(); }

        await using var asB = _fixture.Create(PersistenceFixture.TenantB);

        Assert.Empty(await asB.Set<SubProject>().Where(sub => sub.ProjectId == project.Id).ToListAsync());
    }

    [SqlServerFact]
    public async Task A_signed_in_company_can_not_save_a_record_stamped_for_another_company()
    {
        await using var asA = _fixture.Create(PersistenceFixture.TenantA);
        asA.Add(NewProject(PersistenceFixture.TenantB, "Smuggled " + Guid.NewGuid()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => asA.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task Updating_bumps_the_version_and_a_second_stale_save_is_refused_instead_of_overwriting()
    {
        var project = NewProject(PersistenceFixture.TenantA, "Race " + Guid.NewGuid());
        await using (var seed = _fixture.Create(Guid.Empty)) { seed.Add(project); await seed.SaveChangesAsync(); }

        await using var first = _fixture.Create(PersistenceFixture.TenantA);
        await using var second = _fixture.Create(PersistenceFixture.TenantA);
        var one = await first.Set<Project>().SingleAsync(p => p.Id == project.Id);
        var two = await second.Set<Project>().SingleAsync(p => p.Id == project.Id);
        Assert.Equal(0, one.Version);

        one.Start(Now);
        await first.SaveChangesAsync();
        Assert.Equal(1, one.Version);

        two.UpdateDetails("Renamed by the second person", null, null, null, new DateOnly(2026, 1, 5), null, Now);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        await using var check = _fixture.Create(PersistenceFixture.TenantA);
        var stored = await check.Set<Project>().SingleAsync(p => p.Id == project.Id);
        Assert.Equal(ProjectStatus.Active, stored.Status);
        Assert.NotEqual("Renamed by the second person", stored.Name);
    }

    [SqlServerFact]
    public async Task Project_names_are_unique_per_company_but_may_repeat_across_companies()
    {
        var name = "Same name " + Guid.NewGuid();
        await using var seed = _fixture.Create(Guid.Empty);
        seed.Add(NewProject(PersistenceFixture.TenantA, name));
        seed.Add(NewProject(PersistenceFixture.TenantB, name));
        await seed.SaveChangesAsync();

        await using var duplicate = _fixture.Create(Guid.Empty);
        duplicate.Add(NewProject(PersistenceFixture.TenantA, name));
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task Money_quantities_and_dates_round_trip_exactly()
    {
        var project = NewProject(PersistenceFixture.TenantA, "Round trip " + Guid.NewGuid());
        var line = BudgetLine.Create(Guid.NewGuid(), PersistenceFixture.TenantA, project, null, 3, null, null, "3300", "Ready-mix concrete", ItemType.Material,
            123.4567m, "m3", 5_800_00, VatTreatment.Inclusive, 1_200, new DateOnly(2026, 2, 28), "remark", Guid.NewGuid(), null, Now);
        await using (var seed = _fixture.Create(Guid.Empty)) { seed.AddRange(project, line); await seed.SaveChangesAsync(); }

        await using var db = _fixture.Create(PersistenceFixture.TenantA);
        var loaded = await db.Set<BudgetLine>().SingleAsync(item => item.Id == line.Id);

        Assert.Equal(123.4567m, loaded.Quantity);
        Assert.Equal(line.NetMinor, loaded.NetMinor);
        Assert.Equal(line.VatMinor, loaded.VatMinor);
        Assert.Equal(line.GrossMinor, loaded.GrossMinor);
        Assert.Equal(loaded.GrossMinor, loaded.NetMinor + loaded.VatMinor);
        Assert.Equal(new DateOnly(2026, 2, 28), loaded.EntryDate);
        Assert.Equal(Now, loaded.CreatedAtUtc);
        Assert.Equal("m³", loaded.Unit);
        Assert.Equal("Cast-In-Place Concrete", loaded.SubScope);
    }

    [SqlServerFact]
    public async Task The_same_supplier_invoice_can_not_be_posted_twice_for_a_project_until_the_first_is_voided()
    {
        var project = NewProject(PersistenceFixture.TenantA, "Invoices " + Guid.NewGuid());
        CostEntry Entry(string invoice) => CostEntry.RecordManual(Guid.NewGuid(), PersistenceFixture.TenantA, project, null, 3, null, null, "3200", "Rebar",
            ItemType.Material, 10m, "pc", 10_000, VatTreatment.Inclusive, 1_200, new DateOnly(2026, 10, 30), null, "Visayas Steel", invoice, Guid.NewGuid(),
            null, Today, Now);
        var first = Entry("VST-100");
        await using (var seed = _fixture.Create(Guid.Empty)) { seed.AddRange(project, first); await seed.SaveChangesAsync(); }

        await using (var duplicate = _fixture.Create(Guid.Empty))
        {
            duplicate.Add(Entry("vst 100"));
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
        }

        await using (var voiding = _fixture.Create(PersistenceFixture.TenantA))
        {
            (await voiding.Set<CostEntry>().SingleAsync(item => item.Id == first.Id)).Void(Guid.NewGuid(), "Entered against the wrong project", Now);
            await voiding.SaveChangesAsync();
        }

        await using var again = _fixture.Create(Guid.Empty);
        again.Add(Entry("VST-100"));
        await again.SaveChangesAsync();
    }

    [SqlServerFact]
    public async Task A_repeated_client_request_id_is_refused_by_the_database()
    {
        var project = NewProject(PersistenceFixture.TenantA, "Idempotency " + Guid.NewGuid());
        var requestId = Guid.NewGuid().ToString("N");
        BudgetLine Line() => BudgetLine.Create(Guid.NewGuid(), PersistenceFixture.TenantA, project, null, null, null, null, null, "Cement", ItemType.Material, 1m,
            "bag", 300_00, VatTreatment.Inclusive, 1_200, new DateOnly(2026, 2, 1), null, Guid.NewGuid(), requestId, Now);
        await using (var seed = _fixture.Create(Guid.Empty)) { seed.AddRange(project, Line()); await seed.SaveChangesAsync(); }

        await using var db = _fixture.Create(Guid.Empty);
        db.Add(Line());

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}

/// <summary>Numbers handed out for purchase orders, change orders, invoices and the like.</summary>
public sealed class NumberSequenceTests(PersistenceFixture fixture) : IClassFixture<PersistenceFixture>
{
    private async Task<long> NextAsync(Guid tenant, string key)
    {
        await using var db = fixture.Create(tenant);
        return await new Cms.Infrastructure.Persistence.NumberSequenceStore(db, new FixedTenant(tenant)).NextAsync(key, CancellationToken.None);
    }

    [SqlServerFact]
    public async Task Numbers_rise_one_at_a_time_and_each_company_and_sequence_counts_on_its_own()
    {
        var key = "po-" + Guid.NewGuid().ToString("N")[..8];

        Assert.Equal(1, await NextAsync(PersistenceFixture.TenantA, key));
        Assert.Equal(2, await NextAsync(PersistenceFixture.TenantA, key));
        Assert.Equal(1, await NextAsync(PersistenceFixture.TenantB, key));
        Assert.Equal(1, await NextAsync(PersistenceFixture.TenantA, key + "-other"));
        Assert.Equal(3, await NextAsync(PersistenceFixture.TenantA, key));
    }

    [SqlServerFact]
    public async Task Callers_at_the_same_moment_never_get_the_same_number()
    {
        var key = "race-" + Guid.NewGuid().ToString("N")[..8];

        var numbers = await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => Task.Run(() => NextAsync(PersistenceFixture.TenantA, key))));

        Assert.Equal(Enumerable.Range(1, 40).Select(value => (long)value), numbers.Order());
    }

    [SqlServerFact]
    public async Task Nobody_signed_in_gets_no_number()
    {
        await using var db = fixture.Create(Guid.Empty);
        var store = new Cms.Infrastructure.Persistence.NumberSequenceStore(db, new FixedTenant(Guid.Empty));

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.NextAsync("anything", CancellationToken.None));
    }

    private sealed class FixedTenant(Guid tenantId) : Cms.Application.Common.ICurrentUser
    {
        public bool IsAuthenticated => tenantId != Guid.Empty;
        public Guid UserId => Guid.NewGuid();
        public Guid TenantId => tenantId;
        public string UserName => "test";
        public string DisplayName => "Test";
        public Cms.Domain.Features.Identity.AccessRole Roles => Cms.Domain.Features.Identity.AccessRole.Administrator;
        public string? CorrelationId => null;
    }
}
