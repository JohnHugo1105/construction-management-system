using System.Net;
using Cms.Api.Tests.Infrastructure;
using Cms.Application.Common;
using Cms.Application.Features.Inventory;
using Cms.Domain.Common;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Identity;
using Cms.Domain.Features.Inventory;
using Cms.Infrastructure.Persistence;
using Cms.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Cms.Api.Tests.Features.InventoryWorld;

namespace Cms.Api.Tests.Features;

public sealed class InventoryConcurrencyAndPerformanceTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;

    public InventoryConcurrencyAndPerformanceTests(CmsApiFactory factory) => _factory = factory;

    // ---------------------------------------------------------------- the last units

    [SqlServerFact]
    public async Task Two_people_moving_the_last_units_at_the_same_moment_never_both_succeed()
    {
        using var world = await StartAsync(_factory);
        var keeper = await world.ColleagueAsync(AccessRole.WarehouseKeeper);
        var towerA = await world.ProjectAsync("Tower A");
        var mallB = await world.ProjectAsync("Mall B");
        const int rounds = 6;

        for (var round = 0; round < rounds; round++)
        {
            // Both people opened the item at the same version and each asks for all of it (a tool by the dozen, or the one machine).
            var single = round % 3 == 2;
            var item = single
                ? await world.RegisterMachineAsync($"Excavator {round}", $"EX-{round:D4}")
                : await world.RegisterToolAsync($"Claw hammer {round}", 10m);
            var all = single ? 1m : 10m;

            var first = world.Client.PostAsync(Url(item.Id, "transfer"), Move(Warehouse, At(towerA.Id), all, item.Version, charge: true));
            var second = keeper.PostAsync(Url(item.Id, "transfer"), Move(Warehouse, At(mallB.Id), all, item.Version, charge: true));
            var responses = await Task.WhenAll(first, second);

            var statuses = responses.Select(response => response.StatusCode).Order().ToArray();
            Assert.True(statuses.SequenceEqual([HttpStatusCode.OK, HttpStatusCode.Conflict]),
                $"Round {round}: expected one 200 and one 409 but got {string.Join(" and ", statuses.Select(status => (int)status))}.");
            var loser = responses.Single(response => response.StatusCode == HttpStatusCode.Conflict);
            await ExpectAsync(loser, HttpStatusCode.Conflict, "concurrency_conflict");

            // The stock is in exactly one place, the warehouse holds nothing, and the ledger has the receipt plus one move.
            var after = await world.GetAsync(item.Id);
            var row = Assert.Single(after.Balances);
            Assert.Equal(("Project", all), (row.Location, row.Quantity));
            Assert.Contains(row.ProjectId, new Guid?[] { towerA.Id, mallB.Id });
            Assert.Equal(all, after.TotalQuantity);
            var ledger = await world.LedgerAsync(item.Id);
            Assert.Equal(["Transfer", "Purchase"], ledger.Select(line => line.Type));
        }

        // Only the winners were charged: one cost entry per round, each for its winning movement.
        var costs = await world.CostEntriesAsync();
        Assert.Equal(rounds, costs.Count);
        Assert.Equal(rounds, costs.Select(cost => cost.OriginReferenceId).Distinct().Count());
        Assert.All(costs, cost => Assert.Equal(CostEntryStatus.Posted, cost.Status));
    }

    [SqlServerTheory]
    [InlineData(10, 10, false)] // both take all of it, to different projects
    [InlineData(10, 10, true)] // both take all of it, to the same project (both would add the same balance row)
    [InlineData(6, 6, true)] // both take part of it, to the same project
    [InlineData(6, 6, false)] // both take part of it, to different projects
    public async Task The_database_itself_turns_a_second_save_of_the_same_stock_into_a_conflict(int firstQuantity, int secondQuantity, bool sameProject)
    {
        using var world = await StartAsync(_factory);
        var towerA = await world.ProjectAsync("Tower A");
        var mallB = await world.ProjectAsync("Mall B");
        var item = await world.RegisterToolAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Two people each load the item at version N, pass the version check, and change it. Whoever saves second must lose.
        using var scopeOne = _factory.Services.CreateScope();
        using var scopeTwo = _factory.Services.CreateScope();
        var dbOne = scopeOne.ServiceProvider.GetRequiredService<CmsDbContext>();
        var dbTwo = scopeTwo.ServiceProvider.GetRequiredService<CmsDbContext>();
        var one = await LoadAsync(dbOne, item.Id);
        var two = await LoadAsync(dbTwo, item.Id);
        Assert.Equal(one.Version, two.Version);

        one.Transfer(StockLocation.Warehouse, StockLocation.Project(towerA.Id), firstQuantity, today, false, null, world.User.UserId, null, today, DateTimeOffset.UtcNow);
        two.Transfer(StockLocation.Warehouse, StockLocation.Project(sameProject ? towerA.Id : mallB.Id), secondQuantity, today, false, null, world.User.UserId, null, today,
            DateTimeOffset.UtcNow);

        await new UnitOfWork(dbOne).SaveChangesAsync();
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => new UnitOfWork(dbTwo).SaveChangesAsync());

        var after = await world.GetAsync(item.Id);
        Assert.Equal(10m, after.TotalQuantity);
        Assert.Equal(2, (await world.LedgerAsync(item.Id)).Count);
    }

    /// <summary>Every response is one of the allowed statuses; a failure shows what each response actually said.</summary>
    private static async Task AssertOnlyAsync(HttpResponseMessage[] responses, params HttpStatusCode[] allowed)
    {
        var said = new List<string>();
        foreach (var response in responses)
        {
            var body = await response.Content.ReadAsStringAsync();
            said.Add($"{(int)response.StatusCode} {body[..Math.Min(300, body.Length)]}");
        }

        Assert.True(responses.All(response => allowed.Contains(response.StatusCode)), string.Join(" | ", said));
    }

    private static Task<InventoryItem> LoadAsync(CmsDbContext db, Guid itemId) =>
        db.Set<InventoryItem>().IgnoreQueryFilters().Include(item => item.Balances).SingleAsync(item => item.Id == itemId);

    [SqlServerFact]
    public async Task A_double_click_that_sends_the_same_request_twice_at_once_is_applied_once()
    {
        using var world = await StartAsync(_factory);
        var site = await world.ProjectAsync("Tower A");

        for (var round = 0; round < 4; round++)
        {
            // Registering: the same form sent twice. Either the second sees the first and returns it, or the database refuses the duplicate.
            var registerId = Guid.NewGuid().ToString();
            var registered = await Task.WhenAll(
                world.Client.PostAsync(ItemsUrl, MachineBody($"Grader {round}", $"GR-{round:D4}", requestId: registerId)),
                world.Client.PostAsync(ItemsUrl, MachineBody($"Grader {round}", $"GR-{round:D4}", requestId: registerId)));
            await AssertOnlyAsync(registered, HttpStatusCode.Created, HttpStatusCode.Conflict);
            Assert.Contains(registered, response => response.StatusCode == HttpStatusCode.Created);
            var made = (await world.Client.GetAsync<PagedResult<InventoryListItemDto>>($"{ItemsUrl}?search=Grader {round}")).Items;
            var grader = Assert.Single(made);

            // Moving: the same request, with the same version, twice.
            var moveId = Guid.NewGuid().ToString();
            var body = Move(Warehouse, At(site.Id), 1m, (await world.GetAsync(grader.Id)).Version, requestId: moveId);
            var moved = await Task.WhenAll(world.Client.PostAsync(Url(grader.Id, "transfer"), body), world.Client.PostAsync(Url(grader.Id, "transfer"), body));
            await AssertOnlyAsync(moved, HttpStatusCode.OK, HttpStatusCode.Conflict);
            Assert.Contains(moved, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Equal(["Transfer", "Purchase"], (await world.LedgerAsync(grader.Id)).Select(line => line.Type));
        }
    }

    [SqlServerFact]
    public async Task Moving_and_reversing_the_same_stock_at_the_same_moment_leaves_the_books_consistent()
    {
        using var world = await StartAsync(_factory);
        var keeper = await world.ColleagueAsync(AccessRole.WarehouseKeeper);
        var site = await world.ProjectAsync("Tower A");
        var other = await world.ProjectAsync("Mall B");

        for (var round = 0; round < 4; round++)
        {
            var item = await world.RegisterToolAsync($"Level {round}", 10m);
            var onSite = await world.TransferAsync(item, Warehouse, At(site.Id), 10m, charge: true);
            var move = (await world.LedgerAsync(item.Id))[0];

            // One person reverses the move while another moves the same stock on.
            var reverse = world.Client.PostAsync(Url(item.Id, $"movements/{move.Id}/reverse"), Reverse("Wrong site", onSite.Version));
            var moveOn = keeper.PostAsync(Url(item.Id, "transfer"), Move(At(site.Id), At(other.Id), 10m, onSite.Version));
            var responses = await Task.WhenAll(reverse, moveOn);

            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
            var after = await world.GetAsync(item.Id);
            Assert.Equal(10m, after.TotalQuantity);
            var cost = (await world.CostEntriesAsync()).Single(entry => entry.OriginReferenceId == move.Id);
            // The charge is voided exactly when the move was reversed.
            var reversed = (await world.LedgerAsync(item.Id)).Any(line => line.Type == "Reversal");
            Assert.Equal(reversed ? CostEntryStatus.Voided : CostEntryStatus.Posted, cost.Status);
        }
    }

    // ---------------------------------------------------------------- statements per request

    [SqlServerFact]
    public async Task Reading_costs_the_same_number_of_statements_for_five_items_as_for_forty()
    {
        using var world = await StartAsync(_factory);
        var site = await world.ProjectAsync("Tower A");
        var listUrl = $"{ItemsUrl}?pageSize=100";
        var projectUrl = $"/api/projects/{site.Id}/inventory?pageSize=100";

        await world.SeedItemsAsync(site.Id, 5, "Tool");
        var five = await MeasureReadsAsync(world, listUrl, projectUrl);
        await world.SeedItemsAsync(site.Id, 35, "Plant");
        var forty = await MeasureReadsAsync(world, listUrl, projectUrl);

        Assert.Equal((5, 5), (five.ListItems, five.ProjectItems));
        Assert.Equal((40, 40), (forty.ListItems, forty.ProjectItems));
        Assert.All(new[] { five, forty }, measured => Assert.Equal(2, measured.ListStatements));
        Assert.Equal(five.ListStatements, forty.ListStatements);
        Assert.Equal(five.ProjectStatements, forty.ProjectStatements);
        Assert.Equal(five.DetailStatements, forty.DetailStatements);
        // The list is its count plus its page; project stock adds the existence check; the detail is one statement.
        Assert.Equal((3, 1), (forty.ProjectStatements, forty.DetailStatements));
        // Every row came back with its project named, not an id.
        var rows = await world.Client.GetAsync<PagedResult<InventoryListItemDto>>(listUrl);
        Assert.All(rows.Items, item => Assert.Contains(item.Balances, balance => balance.Location == "Project" && balance.ProjectName == "Tower A"));
    }

    [SqlServerFact]
    public async Task The_ledger_costs_the_same_number_of_statements_for_three_lines_as_for_thirty()
    {
        using var world = await StartAsync(_factory);
        var site = await world.ProjectAsync("Tower A");
        var item = await world.RegisterToolAsync(quantity: 50m);
        var url = $"{Url(item.Id, "movements")}?pageSize=100";

        await world.SeedMovementsAsync(item.Id, site.Id, pairs: 1);
        var few = await MeasureLedgerAsync(world, url);
        await world.SeedMovementsAsync(item.Id, site.Id, pairs: 14);
        var many = await MeasureLedgerAsync(world, url);

        Assert.Equal((3, 31), (few.Rows, many.Rows));
        Assert.Equal(few.Statements, many.Statements);
        Assert.True(many.Statements <= 3, $"The ledger took {many.Statements} statements.");
    }

    private static async Task<Measured> MeasureReadsAsync(InventoryWorld world, string listUrl, string projectUrl)
    {
        var itemId = (await world.Client.GetAsync<PagedResult<InventoryListItemDto>>(listUrl)).Items[0].Id;
        var (listItems, listStatements) = await CountStatementsAsync(world, async () => (await world.Client.GetAsync<PagedResult<InventoryListItemDto>>(listUrl)).Items.Count);
        var (projectItems, projectStatements) = await CountStatementsAsync(world, async () => (await world.Client.GetAsync<PagedResult<ProjectStockDto>>(projectUrl)).Items.Count);
        var (_, detailStatements) = await CountStatementsAsync(world, async () => (await world.GetAsync(itemId)).Balances.Count);
        return new Measured(listItems, listStatements, projectItems, projectStatements, detailStatements);
    }

    private static async Task<(int Rows, int Statements)> MeasureLedgerAsync(InventoryWorld world, string url) =>
        await CountStatementsAsync(world, async () => (await world.Client.GetAsync<PagedResult<StockMovementDto>>(url)).Items.Count);

    /// <summary>Runs a read once to warm the session cache, then again while counting the SQL statements it costs.</summary>
    private static async Task<(int Rows, int Statements)> CountStatementsAsync(InventoryWorld world, Func<Task<int>> read)
    {
        await read();
        var rows = 0;
        var statements = await world.Factory.Sql.MeasureAsync(async () => rows = await read());
        return (rows, statements);
    }

    private sealed record Measured(int ListItems, int ListStatements, int ProjectItems, int ProjectStatements, int DetailStatements);
}
