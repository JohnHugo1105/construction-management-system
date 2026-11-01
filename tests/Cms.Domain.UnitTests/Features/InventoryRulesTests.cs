using Cms.Domain.Common;
using Cms.Domain.Features.Inventory;

namespace Cms.Domain.UnitTests.Features;

/// <summary>
/// Rules found while building the inventory application layer: each one closes a way to reach a state the ledger and balances
/// were never meant to hold, or a way a rejected request could leave an item half changed.
/// </summary>
public sealed class InventoryRulesTests
{
    private static readonly DateOnly Bought = new(2026, 3, 1);
    private static readonly DateOnly Later = new(2026, 4, 1);

    private static InventoryItem NewTools(decimal quantity = 10m, long unitCost = 45_000) =>
        InventoryItem.Register(Guid.NewGuid(), TestData.Tenant, "Claw hammer", InventoryCategory.Tool, "Stanley", null, "pcs", "Hardware Depot",
            "INV-1", Bought, unitCost, quantity, null, TestData.UserId, null, TestData.Today, TestData.Now);

    private static InventoryItem NewExcavator() =>
        InventoryItem.Register(Guid.NewGuid(), TestData.Tenant, "Excavator PC200", InventoryCategory.Machinery, "Komatsu", "PC200-8-12345", "unit",
            null, null, Bought, 250_000_000, 1m, null, TestData.UserId, null, TestData.Today, TestData.Now);

    [Fact]
    public void A_note_or_request_id_that_is_too_long_is_refused_before_any_stock_moves()
    {
        var item = NewTools();
        var site = StockLocation.Project(Guid.NewGuid());

        var note = Assert.Throws<ValidationFailedException>(() => item.Transfer(StockLocation.Warehouse, site, 4m, Later, false,
            new string('n', 301), TestData.UserId, null, TestData.Today, TestData.Now));
        var requestId = Assert.Throws<ValidationFailedException>(() => item.Transfer(StockLocation.Warehouse, site, 4m, Later, false,
            null, TestData.UserId, new string('r', 81), TestData.Today, TestData.Now));

        Assert.Contains("note", note.Errors.Keys);
        Assert.Contains("clientRequestId", requestId.Errors.Keys);
        Assert.Equal(10m, item.BalanceAt(StockLocation.Warehouse));
        Assert.Equal(0m, item.BalanceAt(site));
        Assert.Single(item.Balances);
        Assert.Single(item.Movements);
    }

    [Fact]
    public void A_charge_too_large_to_store_is_a_validation_error_and_nothing_moves()
    {
        // Each piece is within the limit, but ten of them charged together are not.
        var item = NewTools(unitCost: MoneyLimits.MaximumAmountMinor);
        var site = StockLocation.Project(Guid.NewGuid());

        var ex = Assert.Throws<ValidationFailedException>(() => item.Transfer(StockLocation.Warehouse, site, 10m, Later, true, null,
            TestData.UserId, null, TestData.Today, TestData.Now));

        Assert.Contains("chargeToProject", ex.Errors.Keys);
        Assert.Equal(10m, item.BalanceAt(StockLocation.Warehouse));
        Assert.Single(item.Movements);
    }

    [Fact]
    public void A_unit_cost_above_the_limit_is_refused_when_the_item_is_registered()
    {
        var ex = Assert.Throws<ValidationFailedException>(() => NewTools(unitCost: 9_000_000_000_000_000_000));
        Assert.Contains("unitCostMinor", ex.Errors.Keys);
    }

    [Fact]
    public void A_single_piece_is_counted_as_zero_or_one_and_nothing_between()
    {
        var excavator = NewExcavator();

        var half = Assert.Throws<ValidationFailedException>(() => excavator.Adjust(StockLocation.Warehouse, 0.5m, "Half there", Later, TestData.UserId, null,
            TestData.Today, TestData.Now));

        Assert.Contains("countedQuantity", half.Errors.Keys);
        Assert.Equal(1m, excavator.BalanceAt(StockLocation.Warehouse));
    }

    [Fact]
    public void A_retired_item_can_not_get_stock_back_by_reversing_a_count()
    {
        var item = NewTools(quantity: 2m);
        var count = item.Adjust(StockLocation.Warehouse, 0m, "Everything was lost in the flood", Later, TestData.UserId, null, TestData.Today, TestData.Now)!;
        item.Retire("No longer used", TestData.Now);

        Assert.Throws<DomainRuleException>(() => item.Reverse(count, "Found them", Later, TestData.UserId, TestData.Today, TestData.Now));

        Assert.Equal(0m, item.TotalQuantity);
        Assert.Equal(InventoryItemStatus.Retired, item.Status);
        Assert.False(count.IsReversed);
    }

    [Fact]
    public void A_single_piece_is_never_recorded_in_two_places_by_a_count()
    {
        var excavator = NewExcavator();
        var site = StockLocation.Project(Guid.NewGuid());

        var ex = Assert.Throws<DomainRuleException>(() => excavator.Adjust(site, 1m, "Seen on site", Later, TestData.UserId, null, TestData.Today, TestData.Now));

        Assert.Contains("another place", ex.Message);
        Assert.Equal(1m, excavator.TotalQuantity);
        Assert.Equal(0m, excavator.BalanceAt(site));
    }

    [Fact]
    public void A_reversal_can_not_put_a_second_copy_of_a_single_piece_back()
    {
        var excavator = NewExcavator();
        var site = StockLocation.Project(Guid.NewGuid());
        var lost = excavator.Adjust(StockLocation.Warehouse, 0m, "Not in the yard", Later, TestData.UserId, null, TestData.Today, TestData.Now)!;
        excavator.Adjust(site, 1m, "Found on site", Later, TestData.UserId, null, TestData.Today, TestData.Now);

        Assert.Throws<DomainRuleException>(() => excavator.Reverse(lost, "Wrong count", Later, TestData.UserId, TestData.Today, TestData.Now));

        Assert.Equal(1m, excavator.TotalQuantity);
        Assert.False(lost.IsReversed);
    }

    [Fact]
    public void A_reversal_keeps_the_request_id_it_was_made_with_and_refuses_one_that_is_too_long()
    {
        var item = NewTools();
        var site = StockLocation.Project(Guid.NewGuid());
        var move = item.Transfer(StockLocation.Warehouse, site, 4m, Later, false, null, TestData.UserId, null, TestData.Today, TestData.Now);

        Assert.Throws<ValidationFailedException>(() => item.Reverse(move, "Wrong site", Later, TestData.UserId, TestData.Today, TestData.Now,
            new string('r', 81)));
        Assert.Equal(4m, item.BalanceAt(site));
        Assert.False(move.IsReversed);

        var reversal = item.Reverse(move, "Wrong site", Later, TestData.UserId, TestData.Today, TestData.Now, "reverse-1");

        Assert.Equal("reverse-1", reversal.ClientRequestId);
        Assert.Equal(10m, item.BalanceAt(StockLocation.Warehouse));
    }
}
