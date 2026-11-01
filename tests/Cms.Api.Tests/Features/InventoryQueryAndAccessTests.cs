using System.Net;
using Cms.Api.Tests.Infrastructure;
using Cms.Application.Common;
using Cms.Application.Features.Inventory;
using Cms.Domain.Features.Identity;
using Cms.TestSupport;
using static Cms.Api.Tests.Features.InventoryWorld;

namespace Cms.Api.Tests.Features;

public sealed class InventoryQueryAndAccessTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;

    public InventoryQueryAndAccessTests(CmsApiFactory factory) => _factory = factory;

    private static async Task<string[]> NamesAsync(InventoryWorld world, string query) =>
        (await world.Client.GetAsync<PagedResult<InventoryListItemDto>>($"{ItemsUrl}?{query}")).Items.Select(item => item.Description).ToArray();

    // ---------------------------------------------------------------- the list

    [SqlServerFact]
    public async Task The_list_filters_by_project_warehouse_category_status_and_search_text()
    {
        using var world = await StartAsync(_factory);
        var towerA = await world.ProjectAsync("Tower A");
        var mallB = await world.ProjectAsync("Mall B");
        var hammer = await world.RegisterToolAsync("Claw hammer", 10m);
        var excavator = await world.RegisterMachineAsync();
        await world.RegisterToolAsync("Hand saw", 3m);
        await world.RegisterToolAsync("Spirit level 50% scale", 2m);
        var drill = await world.RegisterToolAsync("Old drill", 2m);
        await world.TransferAsync(hammer, Warehouse, At(towerA.Id), 4m);
        await world.TransferAsync(excavator, Warehouse, At(mallB.Id), 1m);
        var counted = await world.Client.PostAsync<InventoryItemDto>(Url(drill.Id, "adjust"), Count(Warehouse, 0m, "Lost", drill.Version));
        await world.Client.PostAsync<InventoryItemDto>(Url(drill.Id, "retire"), Retire("Beyond repair", counted.Version));

        Assert.Equal(5, (await world.Client.GetAsync<PagedResult<InventoryListItemDto>>(ItemsUrl)).Total);

        // Items with stock at a project (and the item still reports all its places).
        Assert.Equal(["Claw hammer"], await NamesAsync(world, $"projectId={towerA.Id}"));
        Assert.Equal(["Excavator PC200"], await NamesAsync(world, $"projectId={mallB.Id}"));
        var atTower = Assert.Single((await world.Client.GetAsync<PagedResult<InventoryListItemDto>>($"{ItemsUrl}?projectId={towerA.Id}")).Items);
        Assert.Equal([("Warehouse", 6m), ("Project", 4m)], atTower.Balances.Select(balance => (balance.Location, balance.Quantity)));
        Assert.Equal(10m, atTower.TotalQuantity);

        // Items with stock in the warehouse.
        Assert.Equal(["Claw hammer", "Hand saw", "Spirit level 50% scale"], await NamesAsync(world, "inWarehouse=true"));
        Assert.Equal(5, (await NamesAsync(world, "inWarehouse=false")).Length);

        Assert.Equal(["Excavator PC200"], await NamesAsync(world, "category=Machinery"));
        Assert.Equal(["Claw hammer", "Hand saw", "Old drill", "Spirit level 50% scale"], await NamesAsync(world, "category=tool"));
        Assert.Equal(["Old drill"], await NamesAsync(world, "status=Retired"));
        Assert.Equal(["Claw hammer", "Excavator PC200", "Hand saw", "Spirit level 50% scale"], await NamesAsync(world, "status=Active"));

        Assert.Equal(["Claw hammer"], await NamesAsync(world, "search=HAMMER"));
        Assert.Equal(["Excavator PC200"], await NamesAsync(world, "search=pc200-8"));
        Assert.Equal(["Excavator PC200"], await NamesAsync(world, "search=komatsu"));
        Assert.Equal(["Hand saw"], await NamesAsync(world, "category=Tool&inWarehouse=true&search=saw"));
        // Wildcard characters in the search text are taken literally.
        Assert.Equal(["Spirit level 50% scale"], await NamesAsync(world, "search=50%25"));
        Assert.Equal(["Spirit level 50% scale"], await NamesAsync(world, "search=%25"));
        Assert.Empty(await NamesAsync(world, "search=_"));
        Assert.Empty(await NamesAsync(world, "search=%5B"));

        Assert.Equal(["category"], await InvalidFieldsAsync(await world.Client.GetAsync($"{ItemsUrl}?category=Gadget")));
        Assert.Equal(["status"], await InvalidFieldsAsync(await world.Client.GetAsync($"{ItemsUrl}?status=Lost")));
    }

    [SqlServerFact]
    public async Task The_list_pages_in_a_stable_order_and_reports_the_total()
    {
        using var world = await StartAsync(_factory);
        foreach (var name in new[] { "Chisel", "Drill", "Axe", "Bolt cutter", "Ladder" }) await world.RegisterToolAsync(name, 1m);

        var one = await world.Client.GetAsync<PagedResult<InventoryListItemDto>>($"{ItemsUrl}?pageSize=2&page=1");
        var two = await world.Client.GetAsync<PagedResult<InventoryListItemDto>>($"{ItemsUrl}?pageSize=2&page=2");
        var three = await world.Client.GetAsync<PagedResult<InventoryListItemDto>>($"{ItemsUrl}?pageSize=2&page=3");

        Assert.Equal((5, 2, 1), (one.Total, one.PageSize, one.Page));
        Assert.Equal(["Axe", "Bolt cutter"], one.Items.Select(item => item.Description));
        Assert.Equal(["Chisel", "Drill"], two.Items.Select(item => item.Description));
        Assert.Equal(["Ladder"], three.Items.Select(item => item.Description));
        Assert.Equal(100, (await world.Client.GetAsync<PagedResult<InventoryListItemDto>>($"{ItemsUrl}?pageSize=1000")).PageSize);
    }

    // ---------------------------------------------------------------- what is on a project

    [SqlServerFact]
    public async Task What_is_on_a_project_follows_every_move_and_names_each_piece()
    {
        using var world = await StartAsync(_factory);
        var towerA = await world.ProjectAsync("Tower A");
        var mallB = await world.ProjectAsync("Mall B");
        var hammer = await world.RegisterToolAsync("Claw hammer", 10m);
        var saw = await world.RegisterToolAsync("Hand saw", 5m);
        var excavator = await world.RegisterMachineAsync();
        var hammerOnA = await world.TransferAsync(hammer, Warehouse, At(towerA.Id), 4m);
        await world.TransferAsync(saw, Warehouse, At(towerA.Id), 2m);
        var excavatorOnA = await world.TransferAsync(excavator, Warehouse, At(towerA.Id), 1m);
        var url = $"/api/projects/{towerA.Id}/inventory";

        var onSite = await world.Client.GetAsync<PagedResult<ProjectStockDto>>(url);

        Assert.Equal(3, onSite.Total);
        Assert.Equal([("Claw hammer", "Tool", "pc", 4m), ("Excavator PC200", "Machinery", "unit", 1m), ("Hand saw", "Tool", "pc", 2m)],
            onSite.Items.Select(row => (row.Description, row.Category, row.Unit, row.Quantity)));
        Assert.Equal("PC200-8-12345", onSite.Items.Single(row => row.Description == "Excavator PC200").SerialNumber);
        Assert.Equal("Stanley", onSite.Items.Single(row => row.Description == "Claw hammer").Brand);
        Assert.Equal(hammer.Id, onSite.Items.Single(row => row.Description == "Claw hammer").ItemId);
        Assert.Empty((await world.Client.GetAsync<PagedResult<ProjectStockDto>>($"/api/projects/{mallB.Id}/inventory")).Items);

        // Part of the tools go to the other project, the hammers all come back, the machine is written off.
        await world.TransferAsync(hammerOnA, At(towerA.Id), Warehouse, 4m);
        await world.Client.PostAsync<InventoryItemDto>(Url(excavator.Id, "write-off"), WriteOff(At(towerA.Id), 1m, "Accident", excavatorOnA.Version));
        var after = await world.Client.GetAsync<PagedResult<ProjectStockDto>>(url);
        Assert.Equal(["Hand saw"], after.Items.Select(row => row.Description));

        var paged = await world.Client.GetAsync<PagedResult<ProjectStockDto>>($"{url}?pageSize=1&page=1");
        Assert.Equal((1, 1), (paged.Total, paged.Items.Count));

        // An archived project can still be looked at.
        await world.ArchiveAsync(towerA);
        Assert.Equal(1, (await world.Client.GetAsync<PagedResult<ProjectStockDto>>(url)).Total);
    }

    // ---------------------------------------------------------------- who may do what

    [SqlServerFact]
    public async Task Roles_decide_who_reads_and_who_changes_the_warehouse()
    {
        using var world = await StartAsync(_factory);
        var site = await world.ProjectAsync("Tower A");
        var hammer = await world.RegisterToolAsync();
        var move = (await world.LedgerAsync(hammer.Id))[0];

        // Read: Administrator, Project Manager, Warehouse Keeper, Cost Engineer, Viewer. Write: Administrator and Warehouse Keeper only.
        var roles = new (AccessRole Role, bool CanRead, bool CanWrite)[]
        {
            (AccessRole.ProjectManager, true, false), (AccessRole.CostEngineer, true, false), (AccessRole.Viewer, true, false),
            (AccessRole.Accountant, false, false), (AccessRole.HrOfficer, false, false)
        };
        foreach (var (role, canRead, _) in roles)
        {
            var person = await world.ColleagueAsync(role);
            var readUrls = new[] { ItemsUrl, Url(hammer.Id), Url(hammer.Id, "movements"), $"/api/projects/{site.Id}/inventory" };
            foreach (var url in readUrls)
                Assert.Equal(canRead ? HttpStatusCode.OK : HttpStatusCode.Forbidden, (await person.GetAsync(url)).StatusCode);

            var writes = new (string Method, string Url, object Body)[]
            {
                ("POST", ItemsUrl, ToolBody("Nope")),
                ("PUT", Url(hammer.Id), UpdateBody(hammer, brand: "Nope")),
                ("POST", Url(hammer.Id, "receive"), Receive(1m, hammer.Version)),
                ("POST", Url(hammer.Id, "transfer"), Move(Warehouse, At(site.Id), 1m, hammer.Version)),
                ("POST", Url(hammer.Id, "adjust"), Count(Warehouse, 5m, "Count", hammer.Version)),
                ("POST", Url(hammer.Id, "write-off"), WriteOff(Warehouse, 1m, "Broken", hammer.Version)),
                ("POST", Url(hammer.Id, $"movements/{move.Id}/reverse"), Reverse("Oops", hammer.Version)),
                ("POST", Url(hammer.Id, "retire"), Retire("Obsolete", hammer.Version))
            };
            foreach (var (method, url, body) in writes)
            {
                var response = method == "PUT" ? await person.PutAsync(url, body) : await person.PostAsync(url, body);
                Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{role} {method} {url} returned {(int)response.StatusCode}");
            }
        }

        // Nothing the refused people tried changed anything.
        var after = await world.GetAsync(hammer.Id);
        Assert.Equal((hammer.Version, "Claw hammer", 10m), (after.Version, after.Description, after.TotalQuantity));

        // The warehouse keeper does the work.
        var keeper = await world.ColleagueAsync(AccessRole.WarehouseKeeper);
        var machine = await keeper.PostAsync<InventoryItemDto>(ItemsUrl, MachineBody());
        var moved = await keeper.PostAsync<InventoryItemDto>(Url(machine.Id, "transfer"), Move(Warehouse, At(site.Id), 1m, machine.Version));
        Assert.Equal(1m, QuantityAt(moved, "Project", site.Id));
        Assert.Equal(HttpStatusCode.OK, (await keeper.GetAsync(ItemsUrl)).StatusCode);
    }

    [SqlServerFact]
    public async Task Nobody_signed_out_can_reach_the_warehouse()
    {
        using var world = await StartAsync(_factory);
        using var stranger = new ApiClient(_factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync(ItemsUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync($"/api/projects/{Guid.NewGuid()}/inventory")).StatusCode);
    }

    // ---------------------------------------------------------------- another company

    [SqlServerFact]
    public async Task One_company_can_never_see_or_change_anothers_stock()
    {
        using var mine = await StartAsync(_factory);
        using var theirs = await StartAsync(_factory);
        var site = await mine.ProjectAsync("Secret site");
        var hammer = await mine.RegisterToolAsync("Secret hammer");
        var moved = await mine.TransferAsync(hammer, Warehouse, At(site.Id), 4m, charge: true);
        var move = (await mine.LedgerAsync(hammer.Id))[0];
        var theirOwn = await theirs.RegisterToolAsync("Their own hammer");

        Assert.DoesNotContain((await theirs.Client.GetAsync<PagedResult<InventoryListItemDto>>(ItemsUrl)).Items, item => item.Id == hammer.Id);
        Assert.Equal(["Their own hammer"], await NamesAsync(theirs, "pageSize=100"));
        Assert.Empty(await NamesAsync(theirs, "search=Secret"));
        Assert.Empty(await NamesAsync(theirs, $"projectId={site.Id}"));

        var attempts = new (string Name, Func<Task<HttpResponseMessage>> Call)[]
        {
            ("detail", () => theirs.Client.GetAsync(Url(hammer.Id))),
            ("movements", () => theirs.Client.GetAsync(Url(hammer.Id, "movements"))),
            ("update", () => theirs.Client.PutAsync(Url(hammer.Id), UpdateBody(moved, brand: "Hijacked"))),
            ("receive", () => theirs.Client.PostAsync(Url(hammer.Id, "receive"), Receive(5m, moved.Version))),
            ("transfer", () => theirs.Client.PostAsync(Url(hammer.Id, "transfer"), Move(Warehouse, At(site.Id), 1m, moved.Version))),
            ("adjust", () => theirs.Client.PostAsync(Url(hammer.Id, "adjust"), Count(Warehouse, 1m, "Count", moved.Version))),
            ("write-off", () => theirs.Client.PostAsync(Url(hammer.Id, "write-off"), WriteOff(Warehouse, 1m, "Broken", moved.Version))),
            ("reverse", () => theirs.Client.PostAsync(Url(hammer.Id, $"movements/{move.Id}/reverse"), Reverse("Oops", moved.Version))),
            ("retire", () => theirs.Client.PostAsync(Url(hammer.Id, "retire"), Retire("Obsolete", moved.Version))),
            ("project stock", () => theirs.Client.GetAsync($"/api/projects/{site.Id}/inventory"))
        };
        foreach (var (name, call) in attempts)
        {
            var response = await call();
            Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{name} returned {(int)response.StatusCode}");
        }

        // Their own item can't be moved onto my project either, and my item is exactly as it was.
        var toMyProject = await theirs.Client.PostAsync(Url(theirOwn.Id, "transfer"), Move(Warehouse, At(site.Id), 1m, theirOwn.Version));
        Assert.Equal(["toLocation"], await InvalidFieldsAsync(toMyProject));
        var intact = await mine.GetAsync(hammer.Id);
        Assert.Equal((moved.Version, "Secret hammer", 4m), (intact.Version, intact.Description, QuantityAt(intact, "Project", site.Id)));
        Assert.Empty(await theirs.CostEntriesAsync());
        Assert.Single(await mine.CostEntriesAsync());
    }
}
