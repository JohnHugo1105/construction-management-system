using System.Net;
using Cms.Api.Tests.Infrastructure;
using Cms.Application.Common;
using Cms.Application.Features.Inventory;
using Cms.Domain.Features.Costing;
using Cms.Infrastructure.Persistence;
using Cms.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Cms.Api.Tests.Features.InventoryWorld;

namespace Cms.Api.Tests.Features;

public sealed class InventoryApiTests : IClassFixture<CmsApiFactory>
{
    private readonly CmsApiFactory _factory;

    public InventoryApiTests(CmsApiFactory factory) => _factory = factory;

    // ---------------------------------------------------------------- registering

    [SqlServerFact]
    public async Task A_tool_and_a_machine_are_registered_with_their_first_stock_in_the_warehouse()
    {
        using var world = await StartAsync(_factory);

        var created = await world.Client.PostAsync(ItemsUrl, ToolBody(unit: "pcs"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var hammer = await world.Client.ReadAsync<InventoryItemDto>(created);
        Assert.Equal($"/api/inventory/items/{hammer.Id}", created.Headers.Location!.ToString());
        Assert.Equal("Tool", hammer.Category);
        Assert.Equal("pc", hammer.Unit);
        Assert.Equal(10m, hammer.TotalQuantity);
        Assert.Equal(45_000, hammer.UnitCostMinor);
        Assert.Equal("Active", hammer.Status);
        var shelf = Assert.Single(hammer.Balances);
        Assert.Equal(("Warehouse", (Guid?)null, 10m), (shelf.Location, shelf.ProjectId, shelf.Quantity));

        var excavator = await world.RegisterMachineAsync();
        Assert.Equal("Machinery", excavator.Category);
        Assert.Equal("PC200-8-12345", excavator.SerialNumber);
        Assert.Equal(1m, excavator.TotalQuantity);

        var list = await world.Client.GetAsync<PagedResult<InventoryListItemDto>>(ItemsUrl);
        Assert.Equal(["Claw hammer", "Excavator PC200"], list.Items.Select(item => item.Description));

        var ledger = await world.LedgerAsync(hammer.Id);
        var receipt = Assert.Single(ledger);
        Assert.Equal("Purchase", receipt.Type);
        Assert.Null(receipt.FromName);
        Assert.Equal("Warehouse", receipt.ToName);
        Assert.Equal(10m, receipt.Quantity);
        Assert.Equal(Bought, receipt.Date);
        Assert.Equal("Test Admin", receipt.RecordedBy);
    }

    [SqlServerFact]
    public async Task Units_are_folded_into_one_spelling_when_registering_and_editing()
    {
        using var world = await StartAsync(_factory);

        var bags = await world.Client.PostAsync<InventoryItemDto>(ItemsUrl, ToolBody("Cement bag rack", unit: "BAGS"));
        var pieces = await world.Client.PostAsync<InventoryItemDto>(ItemsUrl, ToolBody("Chisel", unit: "pieces"));
        Assert.Equal(["bag", "pc"], new[] { bags.Unit, pieces.Unit });

        var edited = await world.Client.PutAsync<InventoryItemDto>(Url(pieces.Id), UpdateBody(pieces, unit: "Sets"));
        Assert.Equal("set", edited.Unit);
    }

    [SqlServerFact]
    public async Task Registering_checks_every_field_and_says_which_ones_are_wrong()
    {
        using var world = await StartAsync(_factory);

        var blank = await world.Client.PostAsync(ItemsUrl,
            new { description = " ", category = "Tool", unit = " ", purchaseDate = "0001-01-01", unitCostMinor = -1, quantity = 5 });
        Assert.Equal(["description", "purchaseDate", "unit", "unitCostMinor"], (await InvalidFieldsAsync(blank)).Order());

        var future = await world.Client.PostAsync(ItemsUrl, new { description = "Drill", category = "Tool", unit = "pc", purchaseDate = Day(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3)),
            unitCostMinor = 100, quantity = 1 });
        Assert.Contains("purchaseDate", await InvalidFieldsAsync(future));

        Assert.Contains("category", await InvalidFieldsAsync(await world.Client.PostAsync(ItemsUrl,
            new { description = "Gadget", category = "Gadget", unit = "pc", purchaseDate = Bought, unitCostMinor = 100, quantity = 1 })));
        Assert.Contains("description", await InvalidFieldsAsync(await world.Client.PostAsync(ItemsUrl, ToolBody(new string('d', 201)))));

        foreach (var quantity in new[] { 0m, -2m, 1.23456m })
            Assert.Contains("quantity", await InvalidFieldsAsync(await world.Client.PostAsync(ItemsUrl, ToolBody(quantity: quantity))));

        var noSerial = await world.Client.PostAsync(ItemsUrl, MachineBody(serial: " "));
        Assert.Contains("serialNumber", await InvalidFieldsAsync(noSerial));
        var twoMachines = await world.Client.PostAsync(ItemsUrl, new { description = "Grader", category = "Machinery", serialNumber = "G-1", unit = "unit", purchaseDate = Bought,
            unitCostMinor = 100, quantity = 2 });
        Assert.Contains("quantity", await InvalidFieldsAsync(twoMachines));

        Assert.Empty((await world.Client.GetAsync<PagedResult<InventoryListItemDto>>(ItemsUrl)).Items);
    }

    [SqlServerFact]
    public async Task A_serial_number_belongs_to_one_piece_however_it_is_typed_and_only_within_a_company()
    {
        using var world = await StartAsync(_factory);
        using var other = await StartAsync(_factory);
        var first = await world.RegisterMachineAsync("Excavator PC200", "PC200-8-12345");

        var duplicate = await world.Client.PostAsync(ItemsUrl, MachineBody("Second excavator", "pc200 8 12345"));
        Assert.Equal(["serialNumber"], await InvalidFieldsAsync(duplicate));

        var second = await world.RegisterMachineAsync("Bulldozer D65", "D65-0001");
        var clash = await world.Client.PutAsync(Url(second.Id), UpdateBody(second, serial: "PC-200-8-12345"));
        Assert.Equal(["serialNumber"], await InvalidFieldsAsync(clash));
        // Saving a piece with its own serial number is not a clash.
        var unchanged = await world.Client.PutAsync<InventoryItemDto>(Url(second.Id), UpdateBody(second, brand: "Komatsu"));
        Assert.Equal("Komatsu", unchanged.Brand);

        // The same number in another company is a different piece.
        var theirs = await other.RegisterMachineAsync("Excavator PC200", "PC200-8-12345");
        Assert.NotEqual(first.Id, theirs.Id);
    }

    [SqlServerFact]
    public async Task Registering_twice_with_the_same_request_id_gives_back_the_first_result()
    {
        using var world = await StartAsync(_factory);
        var requestId = Guid.NewGuid().ToString();

        var first = await world.Client.PostAsync<InventoryItemDto>(ItemsUrl, ToolBody(requestId: requestId));
        var again = await world.Client.PostAsync<InventoryItemDto>(ItemsUrl, ToolBody(requestId: requestId));

        Assert.Equal(first.Id, again.Id);
        Assert.Equal(1, (await world.Client.GetAsync<PagedResult<InventoryListItemDto>>(ItemsUrl)).Total);
        Assert.Single(await world.LedgerAsync(first.Id));
        Assert.Single(await world.AuditAsync(first.Id));
    }

    [SqlServerFact]
    public async Task A_request_id_from_another_company_never_returns_that_companys_item()
    {
        using var mine = await StartAsync(_factory);
        using var theirs = await StartAsync(_factory);
        var requestId = Guid.NewGuid().ToString();
        var original = await mine.Client.PostAsync<InventoryItemDto>(ItemsUrl, ToolBody("Mine", requestId: requestId));

        var copy = await theirs.Client.PostAsync<InventoryItemDto>(ItemsUrl, ToolBody("Theirs", requestId: requestId));

        Assert.NotEqual(original.Id, copy.Id);
        Assert.Equal("Theirs", copy.Description);
    }

    [SqlServerFact]
    public async Task Editing_changes_the_details_but_not_the_category_and_a_retired_item_is_locked()
    {
        using var world = await StartAsync(_factory);
        var hammer = await world.RegisterToolAsync();

        var edited = await world.Client.PutAsync<InventoryItemDto>(Url(hammer.Id), UpdateBody(hammer, description: "Claw hammer 16oz", brand: "Estwing", unitCostMinor: 52_500));
        Assert.Equal(("Claw hammer 16oz", "Estwing", 52_500L, "Tool"), (edited.Description, edited.Brand, edited.UnitCostMinor, edited.Category));
        Assert.True(edited.Version > hammer.Version);
        Assert.Equal(10m, edited.TotalQuantity);

        var invalid = await world.Client.PutAsync(Url(hammer.Id), UpdateBody(edited, description: " ", unitCostMinor: -5));
        Assert.Equal(["description", "unitCostMinor"], (await InvalidFieldsAsync(invalid)).Order());

        var written = await world.Client.PostAsync<InventoryItemDto>(Url(hammer.Id, "write-off"), WriteOff(Warehouse, 10m, "All stolen", edited.Version));
        var retired = await world.Client.PostAsync<InventoryItemDto>(Url(hammer.Id, "retire"), Retire("No longer used", written.Version));
        Assert.Equal("Retired", retired.Status);
        Assert.Equal("No longer used", retired.RetiredReason);
        Assert.Contains("retired", await RuleDetailAsync(await world.Client.PutAsync(Url(hammer.Id), UpdateBody(retired, brand: "Stanley"))));
    }

    // ---------------------------------------------------------------- receiving

    [SqlServerFact]
    public async Task A_tool_can_be_restocked_but_a_single_piece_cannot()
    {
        using var world = await StartAsync(_factory);
        var hammer = await world.RegisterToolAsync();
        var excavator = await world.RegisterMachineAsync();

        var restocked = await world.Client.PostAsync<InventoryItemDto>(Url(hammer.Id, "receive"), Receive(5m, hammer.Version));
        Assert.Equal(15m, restocked.TotalQuantity);
        Assert.Equal(15m, QuantityAt(restocked, "Warehouse"));
        Assert.Equal("Purchase", (await world.LedgerAsync(hammer.Id))[0].Type);

        Assert.Contains("single piece", await RuleDetailAsync(await world.Client.PostAsync(Url(excavator.Id, "receive"), Receive(1m, excavator.Version))));
        Assert.Contains("quantity", await InvalidFieldsAsync(await world.Client.PostAsync(Url(hammer.Id, "receive"), Receive(0m, restocked.Version))));
        Assert.Contains("date", await InvalidFieldsAsync(await world.Client.PostAsync(Url(hammer.Id, "receive"),
            Receive(1m, restocked.Version, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3)))));
        Assert.Contains("date", await InvalidFieldsAsync(await world.Client.PostAsync(Url(hammer.Id, "receive"), Receive(1m, restocked.Version, Bought.AddDays(-1)))));
    }

    // ---------------------------------------------------------------- transfers

    [SqlServerFact]
    public async Task A_transfer_moves_stock_between_places_and_every_balance_is_exact()
    {
        using var world = await StartAsync(_factory);
        var towerA = await world.ProjectAsync("Tower A");
        var mallB = await world.ProjectAsync("Mall B");
        var hammer = await world.RegisterToolAsync();

        var afterFirst = await world.TransferAsync(hammer, Warehouse, At(towerA.Id), 4m);
        Assert.Equal(6m, QuantityAt(afterFirst, "Warehouse"));
        Assert.Equal(4m, QuantityAt(afterFirst, "Project", towerA.Id));
        Assert.Equal(10m, afterFirst.TotalQuantity);

        var afterSecond = await world.TransferAsync(afterFirst, At(towerA.Id), At(mallB.Id), 1m);
        Assert.Equal([("Warehouse", 6m), ("Project", 1m), ("Project", 3m)], afterSecond.Balances.Select(balance => (balance.Location, balance.Quantity)));
        Assert.Equal(["Mall B", "Tower A"], afterSecond.Balances.Where(balance => balance.ProjectName is not null).Select(balance => balance.ProjectName!));
        Assert.Equal(10m, afterSecond.TotalQuantity);

        var listed = Assert.Single((await world.Client.GetAsync<PagedResult<InventoryListItemDto>>(ItemsUrl)).Items);
        Assert.Equal(afterSecond.Balances, listed.Balances);
        Assert.Equal(10m, listed.TotalQuantity);

        var ledger = await world.LedgerAsync(hammer.Id);
        Assert.Equal(["Transfer", "Transfer", "Purchase"], ledger.Select(line => line.Type));
        Assert.Equal(("Tower A", "Mall B", 1m), (ledger[0].FromName!, ledger[0].ToName!, ledger[0].Quantity));
        Assert.Equal(("Warehouse", "Tower A", 4m), (ledger[1].FromName!, ledger[1].ToName!, ledger[1].Quantity));
        Assert.All(ledger.Take(2), line => Assert.Equal(0, line.ChargedAmountMinor));
    }

    [SqlServerFact]
    public async Task Moving_everything_out_leaves_no_empty_row_behind()
    {
        using var world = await StartAsync(_factory);
        var site = await world.ProjectAsync("Tower A");
        var hammer = await world.RegisterToolAsync();

        var moved = await world.TransferAsync(hammer, Warehouse, At(site.Id), 10m);

        var only = Assert.Single(moved.Balances);
        Assert.Equal((site.Id, 10m), (only.ProjectId, only.Quantity));
        Assert.Equal(0m, QuantityAt(moved, "Warehouse"));
    }

    [SqlServerFact]
    public async Task Asking_for_more_than_a_place_holds_is_refused_in_plain_words_and_nothing_changes()
    {
        using var world = await StartAsync(_factory);
        var site = await world.ProjectAsync("Tower A");
        var hammer = await world.RegisterToolAsync();

        var tooMany = await world.Client.PostAsync(Url(hammer.Id, "transfer"), Move(Warehouse, At(site.Id), 11m, hammer.Version));
        var detail = await RuleDetailAsync(tooMany);
        Assert.Equal("Only 10 pc are there, but you asked for 11.", detail);
        var fromEmpty = await world.Client.PostAsync(Url(hammer.Id, "transfer"), Move(At(site.Id), Warehouse, 1m, hammer.Version));
        Assert.Equal("There is none of this item there to move.", await RuleDetailAsync(fromEmpty));

        var after = await world.GetAsync(hammer.Id);
        Assert.Equal(hammer.Version, after.Version);
        Assert.Equal(hammer.Balances, after.Balances);
        Assert.Single(await world.LedgerAsync(hammer.Id));
        Assert.Empty(await world.CostEntriesAsync());
    }

    [SqlServerFact]
    public async Task A_transfer_checks_the_places_the_quantity_the_date_and_the_note()
    {
        using var world = await StartAsync(_factory);
        var site = await world.ProjectAsync("Tower A");
        var hammer = await world.RegisterToolAsync();
        var url = Url(hammer.Id, "transfer");

        var nothing = await world.Client.PostAsync(url, new { quantity = 1, version = hammer.Version });
        Assert.Equal(["fromLocation", "toLocation"], (await InvalidFieldsAsync(nothing)).Order());
        var unknownKind = await world.Client.PostAsync(url, Move(new { kind = "Moon" }, At(site.Id), 1m, hammer.Version));
        Assert.Equal(["fromLocation"], await InvalidFieldsAsync(unknownKind));
        var noProject = await world.Client.PostAsync(url, Move(Warehouse, new { kind = "Project" }, 1m, hammer.Version));
        Assert.Equal(["toLocation"], await InvalidFieldsAsync(noProject));
        var samePlace = await world.Client.PostAsync(url, Move(Warehouse, Warehouse, 1m, hammer.Version));
        Assert.Equal(["toLocation"], await InvalidFieldsAsync(samePlace));
        var zero = await world.Client.PostAsync(url, Move(Warehouse, At(site.Id), 0m, hammer.Version));
        Assert.Equal(["quantity"], await InvalidFieldsAsync(zero));
        var future = await world.Client.PostAsync(url, Move(Warehouse, At(site.Id), 1m, hammer.Version, date: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3)));
        Assert.Equal(["date"], await InvalidFieldsAsync(future));
        var beforePurchase = await world.Client.PostAsync(url, Move(Warehouse, At(site.Id), 1m, hammer.Version, date: Bought.AddDays(-1)));
        Assert.Equal(["date"], await InvalidFieldsAsync(beforePurchase));
        var longNote = await world.Client.PostAsync(url, Move(Warehouse, At(site.Id), 4m, hammer.Version, note: new string('n', 301)));
        Assert.Equal(["note"], await InvalidFieldsAsync(longNote));

        // None of the refused requests moved anything.
        var after = await world.GetAsync(hammer.Id);
        Assert.Equal(hammer.Version, after.Version);
        Assert.Equal(hammer.Balances, after.Balances);
        Assert.Single(await world.LedgerAsync(hammer.Id));
    }

    [SqlServerFact]
    public async Task A_project_from_another_company_or_that_does_not_exist_cannot_be_a_place()
    {
        using var world = await StartAsync(_factory);
        using var other = await StartAsync(_factory);
        var theirProject = await other.ProjectAsync("Their site");
        var hammer = await world.RegisterToolAsync();

        foreach (var projectId in new[] { theirProject.Id, Guid.NewGuid() })
        {
            var response = await world.Client.PostAsync(Url(hammer.Id, "transfer"), Move(Warehouse, At(projectId), 1m, hammer.Version));
            Assert.Equal(["toLocation"], await InvalidFieldsAsync(response));
            Assert.Equal(["location"], await InvalidFieldsAsync(await world.Client.PostAsync(Url(hammer.Id, "adjust"), Count(At(projectId), 1m, "Count", hammer.Version))));
        }

        Assert.Equal(hammer.Version, (await world.GetAsync(hammer.Id)).Version);
    }

    [SqlServerFact]
    public async Task Stock_cannot_be_moved_onto_an_archived_project_but_can_always_be_taken_off_one()
    {
        using var world = await StartAsync(_factory);
        var finished = await world.ProjectAsync("Finished job");
        var hammer = await world.RegisterToolAsync();
        var onSite = await world.TransferAsync(hammer, Warehouse, At(finished.Id), 6m);
        finished = await world.ArchiveAsync(finished);

        // Not onto it, charged or not.
        foreach (var charge in new[] { false, true })
        {
            var refused = await world.Client.PostAsync(Url(hammer.Id, "transfer"), Move(Warehouse, At(finished.Id), 1m, onSite.Version, charge));
            Assert.Contains("archived", await RuleDetailAsync(refused));
        }

        Assert.Empty(await world.CostEntriesAsync());
        Assert.Equal(onSite.Version, (await world.GetAsync(hammer.Id)).Version);

        // Off it: the warehouse keeper (who can't restore a project) must still be able to empty a finished site.
        var returned = await world.TransferAsync(onSite, At(finished.Id), Warehouse, 6m);
        Assert.Equal(10m, QuantityAt(returned, "Warehouse"));
        Assert.Single(returned.Balances);

        // Undoing that return would put the stock back onto the archived project, so it is refused too.
        var returnLine = (await world.LedgerAsync(hammer.Id))[0];
        var undo = await world.Client.PostAsync(Url(hammer.Id, $"movements/{returnLine.Id}/reverse"), Reverse("Returned by mistake", returned.Version));
        Assert.Contains("archived", await RuleDetailAsync(undo));
    }

    // ---------------------------------------------------------------- charged transfers

    [SqlServerFact]
    public async Task A_charged_transfer_posts_exactly_one_cost_in_the_same_save_and_reversing_it_voids_that_cost()
    {
        using var world = await StartAsync(_factory);
        var site = await world.ProjectAsync("Tower A");
        var hammer = await world.RegisterToolAsync(unitCostMinor: 45_000);
        var moveDate = Bought.AddDays(5);

        var charged = await world.TransferAsync(hammer, Warehouse, At(site.Id), 4m, charge: true, date: moveDate);

        Assert.Equal(4m, QuantityAt(charged, "Project", site.Id));
        var movement = (await world.LedgerAsync(hammer.Id))[0];
        Assert.Equal(180_000, movement.ChargedAmountMinor);
        var cost = Assert.Single(await world.CostEntriesAsync());
        Assert.Equal(CostOrigin.Inventory, cost.Origin);
        Assert.Equal(movement.Id, cost.OriginReferenceId);
        Assert.Equal(site.Id, cost.ProjectId);
        Assert.Equal("Equipment charged: Claw hammer x 4 (from Warehouse)", cost.Description);
        Assert.Equal(ItemType.Equipment, cost.ItemType);
        Assert.Equal((1m, "l.s", 180_000L), (cost.Quantity, cost.Unit, cost.UnitCostMinor));
        Assert.Equal((VatTreatment.Inclusive, 1_200), (cost.VatTreatment, cost.VatRateBps));
        Assert.Equal((160_714L, 19_286L, 180_000L), (cost.NetMinor, cost.VatMinor, cost.GrossMinor));
        Assert.Equal(moveDate, cost.EntryDate);
        Assert.Equal(world.User.UserId, cost.CreatedByUserId);
        Assert.Equal(CostEntryStatus.Posted, cost.Status);

        var reversed = await world.Client.PostAsync<InventoryItemDto>(Url(hammer.Id, $"movements/{movement.Id}/reverse"),
            Reverse("Sent to the wrong site", charged.Version));
        Assert.Equal(10m, QuantityAt(reversed, "Warehouse"));
        Assert.Single(reversed.Balances);
        var voided = Assert.Single(await world.CostEntriesAsync());
        Assert.Equal(cost.Id, voided.Id);
        Assert.Equal(CostEntryStatus.Voided, voided.Status);
        Assert.Equal("Sent to the wrong site", voided.VoidReason);
        Assert.Equal(world.User.UserId, voided.VoidedByUserId);
        Assert.Equal(180_000, voided.GrossMinor);

        var ledger = await world.LedgerAsync(hammer.Id);
        Assert.Equal(["Reversal", "Transfer", "Purchase"], ledger.Select(line => line.Type));
        Assert.Equal(0, ledger[0].ChargedAmountMinor);
    }

    [SqlServerFact]
    public async Task The_charge_is_the_quantity_times_the_unit_cost_rounded_once_to_the_centavo()
    {
        using var world = await StartAsync(_factory);
        var site = await world.ProjectAsync("Tower A");
        var cable = await world.Client.PostAsync<InventoryItemDto>(ItemsUrl, ToolBody("Extension cable", quantity: 20m, unitCostMinor: 33_333, unit: "m"));

        await world.TransferAsync(cable, Warehouse, At(site.Id), 2.5m, charge: true);

        var cost = Assert.Single(await world.CostEntriesAsync());
        Assert.Equal(83_333, cost.GrossMinor);
        Assert.Equal(83_333, (await world.LedgerAsync(cable.Id))[0].ChargedAmountMinor);
        Assert.Equal("Equipment charged: Extension cable x 2.5 (from Warehouse)", cost.Description);
    }

    [SqlServerFact]
    public async Task A_transfer_between_projects_is_charged_to_the_destination_and_names_the_source()
    {
        using var world = await StartAsync(_factory);
        var towerA = await world.ProjectAsync("Tower A");
        var mallB = await world.ProjectAsync("Mall B");
        var hammer = await world.RegisterToolAsync();
        var onTower = await world.TransferAsync(hammer, Warehouse, At(towerA.Id), 5m);

        await world.TransferAsync(onTower, At(towerA.Id), At(mallB.Id), 2m, charge: true);

        var cost = Assert.Single(await world.CostEntriesAsync());
        Assert.Equal(mallB.Id, cost.ProjectId);
        Assert.Equal("Equipment charged: Claw hammer x 2 (from Tower A)", cost.Description);
        Assert.Equal(90_000, cost.GrossMinor);
    }

    [SqlServerFact]
    public async Task Moving_stock_back_to_the_warehouse_cannot_be_charged_and_an_item_with_no_cost_cannot_be_charged_at_all()
    {
        using var world = await StartAsync(_factory);
        var site = await world.ProjectAsync("Tower A");
        var hammer = await world.RegisterToolAsync();
        var onSite = await world.TransferAsync(hammer, Warehouse, At(site.Id), 4m);

        var toWarehouse = await world.Client.PostAsync(Url(hammer.Id, "transfer"), Move(At(site.Id), Warehouse, 2m, onSite.Version, charge: true));
        Assert.Equal(["chargeToProject"], await InvalidFieldsAsync(toWarehouse));

        var free = await world.RegisterToolAsync("Borrowed ladder", unitCostMinor: 0);
        var noCost = await world.Client.PostAsync(Url(free.Id, "transfer"), Move(Warehouse, At(site.Id), 1m, free.Version, charge: true));
        Assert.Equal(["chargeToProject"], await InvalidFieldsAsync(noCost));

        var tooSmall = await world.RegisterToolAsync("Washer", unitCostMinor: 1);
        var rounds = await world.Client.PostAsync(Url(tooSmall.Id, "transfer"), Move(Warehouse, At(site.Id), 0.0001m, tooSmall.Version, charge: true));
        Assert.Equal(["chargeToProject"], await InvalidFieldsAsync(rounds));

        // A unit cost beyond the limit is refused when the item is registered; one at the limit registers, but ten of them can't be charged.
        var absurd = await world.Client.PostAsync(ItemsUrl, ToolBody("Gold bar", unitCostMinor: 9_000_000_000_000_000_000));
        Assert.Equal(["unitCostMinor"], await InvalidFieldsAsync(absurd));
        var huge = await world.Client.PostAsync<InventoryItemDto>(ItemsUrl, ToolBody("Gold-plated hammer", unitCostMinor: 10_000_000_000_000));
        var overflow = await world.Client.PostAsync(Url(huge.Id, "transfer"), Move(Warehouse, At(site.Id), 10m, huge.Version, charge: true));
        Assert.Equal(["chargeToProject"], await InvalidFieldsAsync(overflow));

        Assert.Empty(await world.CostEntriesAsync());
        Assert.Equal(onSite.Version, (await world.GetAsync(hammer.Id)).Version);
        Assert.Equal(huge.Balances, (await world.GetAsync(huge.Id)).Balances);
    }

    [SqlServerFact]
    public async Task An_uncharged_transfer_posts_no_cost_and_reversing_a_charged_one_tolerates_a_cost_already_voided()
    {
        using var world = await StartAsync(_factory);
        var site = await world.ProjectAsync("Tower A");
        var hammer = await world.RegisterToolAsync();
        var plain = await world.TransferAsync(hammer, Warehouse, At(site.Id), 2m);
        Assert.Empty(await world.CostEntriesAsync());

        var charged = await world.TransferAsync(plain, Warehouse, At(site.Id), 3m, charge: true);
        // The cost is voided from the costing side first.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
            var entry = db.Set<CostEntry>().IgnoreQueryFilters().Single(item => item.TenantId == world.User.TenantId);
            entry.Void(world.User.UserId, "Voided in costing", DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        var chargedLine = (await world.LedgerAsync(hammer.Id))[0];
        var reversed = await world.Client.PostAsync<InventoryItemDto>(Url(hammer.Id, $"movements/{chargedLine.Id}/reverse"), Reverse("Wrong site", charged.Version));

        Assert.Equal(8m, QuantityAt(reversed, "Warehouse"));
        var entryAfter = Assert.Single(await world.CostEntriesAsync());
        Assert.Equal("Voided in costing", entryAfter.VoidReason);
    }

    [SqlServerFact]
    public async Task Reversing_a_charged_transfer_is_refused_while_the_project_it_was_charged_to_is_archived()
    {
        using var world = await StartAsync(_factory);
        var site = await world.ProjectAsync("Tower A");
        var hammer = await world.RegisterToolAsync();
        var charged = await world.TransferAsync(hammer, Warehouse, At(site.Id), 3m, charge: true);
        var line = (await world.LedgerAsync(hammer.Id))[0];
        await world.ArchiveAsync(site);

        var refused = await world.Client.PostAsync(Url(hammer.Id, $"movements/{line.Id}/reverse"), Reverse("Wrong site", charged.Version));

        // The cost on the finished project must not change unnoticed: restore the project first.
        Assert.Contains("Restore it", await RuleDetailAsync(refused));
        Assert.Equal(charged.Version, (await world.GetAsync(hammer.Id)).Version);
        Assert.False(Assert.Single(await world.CostEntriesAsync()).IsVoided);
    }

    // ---------------------------------------------------------------- reversals

    [SqlServerFact]
    public async Task A_movement_can_be_reversed_once_a_reversal_cannot_be_reversed_and_a_reason_is_needed()
    {
        using var world = await StartAsync(_factory);
        var site = await world.ProjectAsync("Tower A");
        var hammer = await world.RegisterToolAsync();
        var moved = await world.TransferAsync(hammer, Warehouse, At(site.Id), 4m);
        var move = (await world.LedgerAsync(hammer.Id))[0];
        var reverseUrl = Url(hammer.Id, $"movements/{move.Id}/reverse");

        Assert.Equal(["reason"], await InvalidFieldsAsync(await world.Client.PostAsync(reverseUrl, Reverse(" ", moved.Version))));

        var reversed = await world.Client.PostAsync<InventoryItemDto>(reverseUrl, Reverse("Sent to the wrong site", moved.Version));
        Assert.Equal(10m, QuantityAt(reversed, "Warehouse"));

        var ledger = await world.LedgerAsync(hammer.Id);
        var reversal = ledger[0];
        Assert.Equal("Reversal", reversal.Type);
        Assert.Equal(move.Id, reversal.ReversalOfMovementId);
        Assert.Equal(("Tower A", "Warehouse", 4m), (reversal.FromName!, reversal.ToName!, reversal.Quantity));
        Assert.Equal("Sent to the wrong site", reversal.Note);
        Assert.True(ledger.Single(line => line.Id == move.Id).IsReversed);
        Assert.False(reversal.IsReversed);

        Assert.Equal("This movement was already reversed.", await RuleDetailAsync(await world.Client.PostAsync(reverseUrl, Reverse("Again", reversed.Version))));
        var undoUndo = await world.Client.PostAsync(Url(hammer.Id, $"movements/{reversal.Id}/reverse"), Reverse("Undo the undo", reversed.Version));
        Assert.Contains("can't be reversed", await RuleDetailAsync(undoUndo));
        Assert.Equal(3, (await world.LedgerAsync(hammer.Id)).Count);
    }

    [SqlServerFact]
    public async Task A_reversal_is_refused_when_the_stock_has_moved_on_and_when_the_movement_is_not_this_items()
    {
        using var world = await StartAsync(_factory);
        var towerA = await world.ProjectAsync("Tower A");
        var mallB = await world.ProjectAsync("Mall B");
        var hammer = await world.RegisterToolAsync();
        var saw = await world.RegisterToolAsync("Hand saw");
        var first = await world.TransferAsync(hammer, Warehouse, At(towerA.Id), 4m);
        var firstMove = (await world.LedgerAsync(hammer.Id))[0];
        var second = await world.TransferAsync(first, At(towerA.Id), At(mallB.Id), 3m);

        var refused = await world.Client.PostAsync(Url(hammer.Id, $"movements/{firstMove.Id}/reverse"), Reverse("Oops", second.Version));
        Assert.Equal("Only 1 pc is there, but you asked for 4.", await RuleDetailAsync(refused));
        Assert.Equal(1m, QuantityAt(await world.GetAsync(hammer.Id), "Project", towerA.Id));
        Assert.Equal(second.Version, (await world.GetAsync(hammer.Id)).Version);

        var wrongItem = await world.Client.PostAsync(Url(saw.Id, $"movements/{firstMove.Id}/reverse"), Reverse("Oops", saw.Version));
        await ExpectAsync(wrongItem, HttpStatusCode.NotFound, "not_found");
        await ExpectAsync(await world.Client.PostAsync(Url(hammer.Id, $"movements/{Guid.NewGuid()}/reverse"), Reverse("Oops", second.Version)),
            HttpStatusCode.NotFound, "not_found");
    }

    [SqlServerFact]
    public async Task Reversing_with_the_same_request_id_twice_reverses_once()
    {
        using var world = await StartAsync(_factory);
        var site = await world.ProjectAsync("Tower A");
        var hammer = await world.RegisterToolAsync();
        var moved = await world.TransferAsync(hammer, Warehouse, At(site.Id), 4m);
        var move = (await world.LedgerAsync(hammer.Id))[0];
        var requestId = Guid.NewGuid().ToString();
        var url = Url(hammer.Id, $"movements/{move.Id}/reverse");

        var first = await world.Client.PostAsync<InventoryItemDto>(url, Reverse("Wrong site", moved.Version, requestId));
        var again = await world.Client.PostAsync<InventoryItemDto>(url, Reverse("Wrong site", moved.Version, requestId));

        Assert.Equal(first.Balances, again.Balances);
        Assert.Equal(first.Version, again.Version);
        Assert.Equal(3, (await world.LedgerAsync(hammer.Id)).Count);
    }

    // ---------------------------------------------------------------- stock counts, write-offs, retiring

    [SqlServerFact]
    public async Task A_stock_count_sets_the_balance_and_records_the_difference_in_either_direction()
    {
        using var world = await StartAsync(_factory);
        var site = await world.ProjectAsync("Tower A");
        var hammer = await world.RegisterToolAsync();

        var down = await world.Client.PostAsync<InventoryItemDto>(Url(hammer.Id, "adjust"), Count(Warehouse, 7m, "Stock count: 3 missing", hammer.Version));
        Assert.Equal(7m, QuantityAt(down, "Warehouse"));
        var up = await world.Client.PostAsync<InventoryItemDto>(Url(hammer.Id, "adjust"), Count(Warehouse, 12m, "Found 5 in the shed", down.Version));
        Assert.Equal(12m, QuantityAt(up, "Warehouse"));
        var onSite = await world.Client.PostAsync<InventoryItemDto>(Url(hammer.Id, "adjust"), Count(At(site.Id), 2m, "Found 2 at the site", up.Version));
        Assert.Equal(14m, onSite.TotalQuantity);

        var ledger = await world.LedgerAsync(hammer.Id);
        Assert.Equal(["Adjustment", "Adjustment", "Adjustment", "Purchase"], ledger.Select(line => line.Type));
        Assert.Equal((null, "Tower A", 2m), (ledger[0].FromName, ledger[0].ToName, ledger[0].Quantity));
        Assert.Equal((null, "Warehouse", 5m), (ledger[1].FromName, ledger[1].ToName, ledger[1].Quantity));
        Assert.Equal(("Warehouse", null, 3m), (ledger[2].FromName, ledger[2].ToName, ledger[2].Quantity));
        Assert.Equal("Stock count: 3 missing", ledger[2].Note);

        Assert.Contains("matches the records", await RuleDetailAsync(await world.Client.PostAsync(Url(hammer.Id, "adjust"),
            Count(Warehouse, 12m, "Same again", onSite.Version))));
        Assert.Equal(["reason"], await InvalidFieldsAsync(await world.Client.PostAsync(Url(hammer.Id, "adjust"), Count(Warehouse, 5m, " ", onSite.Version))));
        Assert.Equal(["countedQuantity"], await InvalidFieldsAsync(await world.Client.PostAsync(Url(hammer.Id, "adjust"), Count(Warehouse, -1m, "Count", onSite.Version))));
        Assert.Equal(["location"], await InvalidFieldsAsync(await world.Client.PostAsync(Url(hammer.Id, "adjust"), new { countedQuantity = 5, reason = "Count", version = onSite.Version })));
    }

    [SqlServerFact]
    public async Task A_single_piece_is_counted_as_zero_or_one_and_is_never_in_two_places()
    {
        using var world = await StartAsync(_factory);
        var site = await world.ProjectAsync("Tower A");
        var excavator = await world.RegisterMachineAsync();

        Assert.Equal(["countedQuantity"], await InvalidFieldsAsync(await world.Client.PostAsync(Url(excavator.Id, "adjust"),
            Count(Warehouse, 2m, "Two found", excavator.Version))));
        var second = await world.Client.PostAsync(Url(excavator.Id, "adjust"), Count(At(site.Id), 1m, "Seen on site", excavator.Version));
        Assert.Contains("another place", await RuleDetailAsync(second));
        Assert.Equal(excavator.Version, (await world.GetAsync(excavator.Id)).Version);
    }

    [SqlServerFact]
    public async Task Writing_off_a_machine_retires_it_and_reversing_the_write_off_restores_it()
    {
        using var world = await StartAsync(_factory);
        var site = await world.ProjectAsync("Tower A");
        var excavator = await world.RegisterMachineAsync();
        var onSite = await world.TransferAsync(excavator, Warehouse, At(site.Id), 1m);

        Assert.Equal(["reason"], await InvalidFieldsAsync(await world.Client.PostAsync(Url(excavator.Id, "write-off"), WriteOff(At(site.Id), 1m, " ", onSite.Version))));
        Assert.Equal(["quantity"], await InvalidFieldsAsync(await world.Client.PostAsync(Url(excavator.Id, "write-off"), WriteOff(At(site.Id), 2m, "Accident", onSite.Version))));
        Assert.Contains("There is none", await RuleDetailAsync(await world.Client.PostAsync(Url(excavator.Id, "write-off"), WriteOff(Warehouse, 1m, "Accident", onSite.Version))));

        var writtenOff = await world.Client.PostAsync<InventoryItemDto>(Url(excavator.Id, "write-off"), WriteOff(At(site.Id), 1m, "Written off after the accident", onSite.Version));
        Assert.Equal("Retired", writtenOff.Status);
        Assert.Equal("Written off after the accident", writtenOff.RetiredReason);
        Assert.Empty(writtenOff.Balances);
        Assert.Contains("retired", await RuleDetailAsync(await world.Client.PostAsync(Url(excavator.Id, "transfer"), Move(At(site.Id), Warehouse, 1m, writtenOff.Version))));
        var retiredList = await world.Client.GetAsync<PagedResult<InventoryListItemDto>>($"{ItemsUrl}?status=Retired");
        Assert.Equal(excavator.Id, Assert.Single(retiredList.Items).Id);

        var writeOffLine = (await world.LedgerAsync(excavator.Id))[0];
        Assert.Equal(("WriteOff", "Tower A", null), (writeOffLine.Type, writeOffLine.FromName, writeOffLine.ToName));
        var restored = await world.Client.PostAsync<InventoryItemDto>(Url(excavator.Id, $"movements/{writeOffLine.Id}/reverse"), Reverse("Insurance repaired it", writtenOff.Version));
        Assert.Equal("Active", restored.Status);
        Assert.Null(restored.RetiredReason);
        Assert.Equal(1m, QuantityAt(restored, "Project", site.Id));
    }

    [SqlServerFact]
    public async Task Writing_off_some_tools_keeps_the_item_active_with_the_rest()
    {
        using var world = await StartAsync(_factory);
        var hammer = await world.RegisterToolAsync();

        var written = await world.Client.PostAsync<InventoryItemDto>(Url(hammer.Id, "write-off"), WriteOff(Warehouse, 2m, "Handles broke", hammer.Version));

        Assert.Equal(("Active", 8m), (written.Status, written.TotalQuantity));
        Assert.Contains("Only 8 pc are there", await RuleDetailAsync(await world.Client.PostAsync(Url(hammer.Id, "write-off"), WriteOff(Warehouse, 9m, "Too many", written.Version))));
    }

    [SqlServerFact]
    public async Task An_item_is_retired_only_when_it_holds_nothing_and_a_retired_item_takes_no_stock_back_by_reversing_a_count()
    {
        using var world = await StartAsync(_factory);
        var hammer = await world.RegisterToolAsync(quantity: 2m);

        Assert.Contains("still has stock", await RuleDetailAsync(await world.Client.PostAsync(Url(hammer.Id, "retire"), Retire("Obsolete", hammer.Version))));

        var counted = await world.Client.PostAsync<InventoryItemDto>(Url(hammer.Id, "adjust"), Count(Warehouse, 0m, "Lost in the flood", hammer.Version));
        Assert.Equal(["reason"], await InvalidFieldsAsync(await world.Client.PostAsync(Url(hammer.Id, "retire"), Retire(" ", counted.Version))));
        var retired = await world.Client.PostAsync<InventoryItemDto>(Url(hammer.Id, "retire"), Retire("Obsolete", counted.Version));
        Assert.Equal(("Retired", "Obsolete"), (retired.Status, retired.RetiredReason));
        Assert.Contains("already retired", await RuleDetailAsync(await world.Client.PostAsync(Url(hammer.Id, "retire"), Retire("Again", retired.Version))));
        Assert.Contains("retired", await RuleDetailAsync(await world.Client.PostAsync(Url(hammer.Id, "receive"), Receive(1m, retired.Version))));

        var countLine = (await world.LedgerAsync(hammer.Id))[0];
        var undo = await world.Client.PostAsync(Url(hammer.Id, $"movements/{countLine.Id}/reverse"), Reverse("Found them", retired.Version));
        Assert.Contains("retired", await RuleDetailAsync(undo));
        Assert.Equal(0m, (await world.GetAsync(hammer.Id)).TotalQuantity);
    }

    // ---------------------------------------------------------------- concurrency of a single editor, idempotency

    [SqlServerFact]
    public async Task A_stale_version_is_refused_by_every_change_and_nothing_is_overwritten()
    {
        using var world = await StartAsync(_factory);
        var site = await world.ProjectAsync("Tower A");
        var hammer = await world.RegisterToolAsync();
        var current = await world.Client.PostAsync<InventoryItemDto>(Url(hammer.Id, "receive"), Receive(1m, hammer.Version));
        var move = (await world.LedgerAsync(hammer.Id))[0];
        var stale = hammer.Version;

        var attempts = new (string Name, Func<Task<HttpResponseMessage>> Call)[]
        {
            ("update", () => world.Client.PutAsync(Url(hammer.Id), UpdateBody(current, description: "Changed", version: stale))),
            ("receive", () => world.Client.PostAsync(Url(hammer.Id, "receive"), Receive(1m, stale))),
            ("transfer", () => world.Client.PostAsync(Url(hammer.Id, "transfer"), Move(Warehouse, At(site.Id), 1m, stale))),
            ("adjust", () => world.Client.PostAsync(Url(hammer.Id, "adjust"), Count(Warehouse, 3m, "Count", stale))),
            ("write-off", () => world.Client.PostAsync(Url(hammer.Id, "write-off"), WriteOff(Warehouse, 1m, "Broken", stale))),
            ("reverse", () => world.Client.PostAsync(Url(hammer.Id, $"movements/{move.Id}/reverse"), Reverse("Oops", stale))),
            ("retire", () => world.Client.PostAsync(Url(hammer.Id, "retire"), Retire("Obsolete", stale)))
        };
        foreach (var (name, call) in attempts)
        {
            var detail = await RuleDetailAsync(await call(), "concurrency_conflict");
            Assert.True(detail.Contains("changed", StringComparison.OrdinalIgnoreCase), name);
        }

        var after = await world.GetAsync(hammer.Id);
        Assert.Equal(current.Version, after.Version);
        Assert.Equal("Claw hammer", after.Description);
        Assert.Equal(11m, after.TotalQuantity);
        Assert.Equal(2, (await world.LedgerAsync(hammer.Id)).Count);
    }

    [SqlServerFact]
    public async Task Repeating_a_transfer_with_the_same_request_id_moves_and_charges_once()
    {
        using var world = await StartAsync(_factory);
        var site = await world.ProjectAsync("Tower A");
        var hammer = await world.RegisterToolAsync();
        var requestId = Guid.NewGuid().ToString();
        var body = Move(Warehouse, At(site.Id), 4m, hammer.Version, charge: true, requestId: requestId);

        var first = await world.Client.PostAsync<InventoryItemDto>(Url(hammer.Id, "transfer"), body);
        // The repeat carries the version the person had opened, which is now stale: it still gets the original result, not a conflict.
        var again = await world.Client.PostAsync<InventoryItemDto>(Url(hammer.Id, "transfer"), body);

        Assert.Equal(first.Balances, again.Balances);
        Assert.Equal(first.Version, again.Version);
        Assert.Equal(2, (await world.LedgerAsync(hammer.Id)).Count);
        Assert.Single(await world.CostEntriesAsync());
    }

    [SqlServerFact]
    public async Task A_request_id_already_used_for_another_item_is_refused()
    {
        using var world = await StartAsync(_factory);
        var site = await world.ProjectAsync("Tower A");
        var hammer = await world.RegisterToolAsync();
        var saw = await world.RegisterToolAsync("Hand saw");
        var requestId = Guid.NewGuid().ToString();
        await world.Client.PostAsync<InventoryItemDto>(Url(hammer.Id, "transfer"), Move(Warehouse, At(site.Id), 1m, hammer.Version, requestId: requestId));

        var reused = await world.Client.PostAsync(Url(saw.Id, "transfer"), Move(Warehouse, At(site.Id), 1m, saw.Version, requestId: requestId));

        Assert.Contains("different item", await RuleDetailAsync(reused));
        Assert.Equal(10m, QuantityAt(await world.GetAsync(saw.Id), "Warehouse"));
    }

    // ---------------------------------------------------------------- the trail

    [SqlServerFact]
    public async Task Every_state_change_is_audited_in_plain_words_naming_the_item_the_quantity_and_the_places()
    {
        using var world = await StartAsync(_factory);
        var towerA = await world.ProjectAsync("Tower A");
        var mallB = await world.ProjectAsync("Mall B");
        var hammer = await world.RegisterToolAsync();
        var received = await world.Client.PostAsync<InventoryItemDto>(Url(hammer.Id, "receive"), Receive(5m, hammer.Version));
        var edited = await world.Client.PutAsync<InventoryItemDto>(Url(hammer.Id), UpdateBody(received, brand: "Estwing"));
        var charged = await world.TransferAsync(edited, Warehouse, At(towerA.Id), 4m, charge: true);
        var chargedLine = (await world.LedgerAsync(hammer.Id))[0];
        var moved = await world.TransferAsync(charged, Warehouse, At(mallB.Id), 1m);
        var counted = await world.Client.PostAsync<InventoryItemDto>(Url(hammer.Id, "adjust"), Count(Warehouse, 8m, "Stock count: 2 missing", moved.Version));
        var writtenOff = await world.Client.PostAsync<InventoryItemDto>(Url(hammer.Id, "write-off"), WriteOff(Warehouse, 8m, "Stolen from the yard", counted.Version));
        var reversed = await world.Client.PostAsync<InventoryItemDto>(Url(hammer.Id, $"movements/{chargedLine.Id}/reverse"), Reverse("Wrong site.", writtenOff.Version));
        var emptiedWarehouse = await world.Client.PostAsync<InventoryItemDto>(Url(hammer.Id, "write-off"), WriteOff(Warehouse, 4m, "Lost", reversed.Version));
        var allGone = await world.Client.PostAsync<InventoryItemDto>(Url(hammer.Id, "write-off"), WriteOff(At(mallB.Id), 1m, "Lost", emptiedWarehouse.Version));
        await world.Client.PostAsync<InventoryItemDto>(Url(hammer.Id, "retire"), Retire("Obsolete", allGone.Version));

        var summaries = (await world.AuditAsync(hammer.Id)).Select(entry => (entry.Action, entry.Summary)).Order().ToList();

        Assert.Equal(
        new[]
        {
            ("Inventory.Registered", "Registered Claw hammer and received 10 pc into the warehouse."),
            ("Inventory.Received", "Received 5 pc of Claw hammer into the warehouse."),
            ("Inventory.Updated", "Updated Claw hammer."),
            ("Inventory.Transferred", "Moved 4 pc of Claw hammer from Warehouse to Tower A. Charged PHP 1,800.00 to the project."),
            ("Inventory.Transferred", "Moved 1 pc of Claw hammer from Warehouse to Mall B."),
            ("Inventory.Adjusted", "Stock count of Claw hammer at Warehouse: counted 8 pc, the records said 10. Reason: Stock count: 2 missing."),
            ("Inventory.WrittenOff", "Wrote off 8 pc of Claw hammer at Warehouse. Reason: Stolen from the yard."),
            ("Inventory.Reversed", "Reversed the move from Warehouse to Tower A (4 pc of Claw hammer). Reason: Wrong site. The PHP 1,800.00 charge to the project was voided."),
            ("Inventory.WrittenOff", "Wrote off 4 pc of Claw hammer at Warehouse. Reason: Lost."),
            ("Inventory.WrittenOff", "Wrote off 1 pc of Claw hammer at Mall B. Reason: Lost."),
            ("Inventory.Retired", "Retired Claw hammer. Reason: Obsolete.")
        }.Order().ToList(), summaries);
        var trail = await world.AuditAsync(hammer.Id);
        Assert.All(trail, entry => Assert.Equal(world.User.Username, entry.UserName));
        Assert.All(trail, entry => Assert.Equal("InventoryItem", entry.EntityType));
    }

    [SqlServerFact]
    public async Task The_ledger_pages_newest_first_and_keeps_every_line()
    {
        using var world = await StartAsync(_factory);
        var site = await world.ProjectAsync("Tower A");
        var hammer = await world.RegisterToolAsync();
        await world.SeedMovementsAsync(hammer.Id, site.Id, pairs: 3);

        var all = await world.LedgerAsync(hammer.Id);
        Assert.Equal(7, all.Count);
        Assert.Equal("Purchase", all[^1].Type);
        Assert.True(all.Zip(all.Skip(1), (newer, older) => newer.RecordedAtUtc >= older.RecordedAtUtc).All(ordered => ordered));

        var first = await world.Client.GetAsync<PagedResult<StockMovementDto>>($"{Url(hammer.Id, "movements")}?pageSize=3&page=1");
        var third = await world.Client.GetAsync<PagedResult<StockMovementDto>>($"{Url(hammer.Id, "movements")}?pageSize=3&page=3");
        Assert.Equal((7, 3, 1), (first.Total, first.Items.Count, third.Items.Count));
        Assert.Equal(all.Take(3).Select(line => line.Id), first.Items.Select(line => line.Id));
        Assert.Equal("Purchase", third.Items[0].Type);
    }

    [SqlServerFact]
    public async Task A_missing_item_is_not_found_whatever_is_asked_of_it()
    {
        using var world = await StartAsync(_factory);
        var missing = Guid.NewGuid();

        await ExpectAsync(await world.Client.GetAsync(Url(missing)), HttpStatusCode.NotFound, "not_found");
        await ExpectAsync(await world.Client.GetAsync(Url(missing, "movements")), HttpStatusCode.NotFound, "not_found");
        await ExpectAsync(await world.Client.PostAsync(Url(missing, "retire"), Retire("Obsolete", 0)), HttpStatusCode.NotFound, "not_found");
        await ExpectAsync(await world.Client.GetAsync($"/api/projects/{missing}/inventory"), HttpStatusCode.NotFound, "not_found");
        Assert.Equal(HttpStatusCode.NotFound, (await world.Client.GetAsync($"{ItemsUrl}/not-a-guid")).StatusCode);
    }
}
