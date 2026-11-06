using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Inventory;

namespace ConstructOPX.Domain.UnitTests.Features;

public sealed class InventoryItemTests
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
    public void Registering_receives_the_first_stock_into_the_warehouse_and_writes_the_ledger()
    {
        var item = NewTools();

        Assert.Equal(10m, item.BalanceAt(StockLocation.Warehouse));
        Assert.Equal(10m, item.TotalQuantity);
        Assert.Equal("pc", item.Unit);
        var movement = Assert.Single(item.Movements);
        Assert.Equal(MovementType.Purchase, movement.Type);
        Assert.Null(movement.From);
        Assert.Equal(StockLocation.Warehouse, movement.To);
    }

    [Fact]
    public void A_transfer_moves_quantity_and_the_balances_still_add_up()
    {
        var item = NewTools();
        var site = StockLocation.Project(Guid.NewGuid());

        item.Transfer(StockLocation.Warehouse, site, 4m, Later, false, null, TestData.UserId, null, TestData.Today, TestData.Now);

        Assert.Equal(6m, item.BalanceAt(StockLocation.Warehouse));
        Assert.Equal(4m, item.BalanceAt(site));
        Assert.Equal(10m, item.TotalQuantity);
    }

    [Fact]
    public void A_location_can_not_give_more_than_it_holds_and_the_failed_attempt_changes_nothing()
    {
        var item = NewTools();
        var site = StockLocation.Project(Guid.NewGuid());

        var ex = Assert.Throws<DomainRuleException>(() => item.Transfer(StockLocation.Warehouse, site, 11m, Later, false, null, TestData.UserId, null,
            TestData.Today, TestData.Now));
        Assert.Contains("Only 10 pc are there", ex.Message);
        Assert.Throws<DomainRuleException>(() => item.Transfer(site, StockLocation.Warehouse, 1m, Later, false, null, TestData.UserId, null, TestData.Today, TestData.Now));

        Assert.Equal(10m, item.BalanceAt(StockLocation.Warehouse));
        Assert.Single(item.Movements);
    }

    [Fact]
    public void Moving_everything_removes_the_empty_balance_row()
    {
        var item = NewTools();
        var site = StockLocation.Project(Guid.NewGuid());

        item.Transfer(StockLocation.Warehouse, site, 10m, Later, false, null, TestData.UserId, null, TestData.Today, TestData.Now);

        Assert.Single(item.Balances);
        Assert.Equal(0m, item.BalanceAt(StockLocation.Warehouse));
    }

    [Fact]
    public void Quantity_date_and_destination_are_validated()
    {
        var item = NewTools();
        var site = StockLocation.Project(Guid.NewGuid());

        Assert.Throws<ValidationFailedException>(() => item.Transfer(StockLocation.Warehouse, site, 0m, Later, false, null, TestData.UserId, null, TestData.Today, TestData.Now));
        Assert.Throws<ValidationFailedException>(() => item.Transfer(StockLocation.Warehouse, site, -2m, Later, false, null, TestData.UserId, null, TestData.Today, TestData.Now));
        Assert.Throws<ValidationFailedException>(() => item.Transfer(StockLocation.Warehouse, StockLocation.Warehouse, 1m, Later, false, null, TestData.UserId, null, TestData.Today, TestData.Now));
        Assert.Throws<ValidationFailedException>(() => item.Transfer(StockLocation.Warehouse, site, 1m, TestData.Today.AddDays(1), false, null, TestData.UserId, null, TestData.Today, TestData.Now));
        Assert.Throws<ValidationFailedException>(() => item.Transfer(StockLocation.Warehouse, site, 1m, Bought.AddDays(-1), false, null, TestData.UserId, null, TestData.Today, TestData.Now));
    }

    [Fact]
    public void Charging_a_transfer_to_a_project_uses_unit_cost_times_quantity()
    {
        var item = NewTools(unitCost: 45_000);
        var site = StockLocation.Project(Guid.NewGuid());

        var movement = item.Transfer(StockLocation.Warehouse, site, 4m, Later, true, null, TestData.UserId, null, TestData.Today, TestData.Now);

        Assert.Equal(180_000, movement.ChargedAmountMinor);
    }

    [Fact]
    public void Moving_stock_back_to_the_warehouse_is_never_charged_and_an_item_without_cost_can_not_be_charged()
    {
        var item = NewTools();
        var site = StockLocation.Project(Guid.NewGuid());
        item.Transfer(StockLocation.Warehouse, site, 4m, Later, false, null, TestData.UserId, null, TestData.Today, TestData.Now);

        Assert.Throws<ValidationFailedException>(() => item.Transfer(site, StockLocation.Warehouse, 2m, Later, true, null, TestData.UserId, null, TestData.Today, TestData.Now));

        var free = NewTools(unitCost: 0);
        Assert.Throws<ValidationFailedException>(() => free.Transfer(StockLocation.Warehouse, site, 1m, Later, true, null, TestData.UserId, null, TestData.Today, TestData.Now));
    }

    [Fact]
    public void A_reversal_puts_stock_back_once_and_marks_the_original()
    {
        var item = NewTools();
        var site = StockLocation.Project(Guid.NewGuid());
        var move = item.Transfer(StockLocation.Warehouse, site, 4m, Later, false, null, TestData.UserId, null, TestData.Today, TestData.Now);

        var reversal = item.Reverse(move, "Sent to the wrong site", Later, TestData.UserId, TestData.Today, TestData.Now);

        Assert.Equal(10m, item.BalanceAt(StockLocation.Warehouse));
        Assert.Equal(0m, item.BalanceAt(site));
        Assert.Equal(MovementType.Reversal, reversal.Type);
        Assert.Equal(move.Id, reversal.ReversalOfMovementId);
        Assert.True(move.IsReversed);
        Assert.Throws<DomainRuleException>(() => item.Reverse(move, "again", Later, TestData.UserId, TestData.Today, TestData.Now));
        Assert.Throws<DomainRuleException>(() => item.Reverse(reversal, "undo the undo", Later, TestData.UserId, TestData.Today, TestData.Now));
    }

    [Fact]
    public void A_reversal_needs_the_stock_to_still_be_where_the_original_put_it_and_a_reason()
    {
        var item = NewTools();
        var site = StockLocation.Project(Guid.NewGuid());
        var other = StockLocation.Project(Guid.NewGuid());
        var move = item.Transfer(StockLocation.Warehouse, site, 4m, Later, false, null, TestData.UserId, null, TestData.Today, TestData.Now);
        item.Transfer(site, other, 3m, Later, false, null, TestData.UserId, null, TestData.Today, TestData.Now);

        Assert.Throws<DomainRuleException>(() => item.Reverse(move, "oops", Later, TestData.UserId, TestData.Today, TestData.Now));
        Assert.Throws<ValidationFailedException>(() => item.Reverse(item.Movements[0], " ", Later, TestData.UserId, TestData.Today, TestData.Now));
    }

    [Fact]
    public void A_stock_count_sets_the_balance_and_records_the_difference()
    {
        var item = NewTools();

        var down = item.Adjust(StockLocation.Warehouse, 7m, "Stock count: 3 missing", Later, TestData.UserId, null, TestData.Today, TestData.Now);
        var up = item.Adjust(StockLocation.Warehouse, 9m, "Found 2 in the shed", Later, TestData.UserId, null, TestData.Today, TestData.Now);

        Assert.Equal(9m, item.BalanceAt(StockLocation.Warehouse));
        Assert.Equal(3m, down!.Quantity);
        Assert.Null(down.To);
        Assert.Equal(2m, up!.Quantity);
        Assert.Null(up.From);
        Assert.Throws<DomainRuleException>(() => item.Adjust(StockLocation.Warehouse, 9m, "same", Later, TestData.UserId, null, TestData.Today, TestData.Now));
        Assert.Throws<ValidationFailedException>(() => item.Adjust(StockLocation.Warehouse, 5m, " ", Later, TestData.UserId, null, TestData.Today, TestData.Now));
    }

    [Fact]
    public void Writing_off_stock_needs_a_reason_and_enough_stock()
    {
        var item = NewTools();

        Assert.Throws<ValidationFailedException>(() => item.WriteOff(StockLocation.Warehouse, 1m, "", Later, TestData.UserId, null, TestData.Today, TestData.Now));
        Assert.Throws<DomainRuleException>(() => item.WriteOff(StockLocation.Warehouse, 11m, "Broken", Later, TestData.UserId, null, TestData.Today, TestData.Now));
        item.WriteOff(StockLocation.Warehouse, 2m, "Handles broke", Later, TestData.UserId, null, TestData.Today, TestData.Now);

        Assert.Equal(8m, item.TotalQuantity);
        Assert.Equal(InventoryItemStatus.Active, item.Status);
    }

    [Fact]
    public void Equipment_and_machinery_need_a_serial_number_and_are_single_pieces()
    {
        Assert.Contains("serialNumber", Assert.Throws<ValidationFailedException>(() => InventoryItem.Register(Guid.NewGuid(), TestData.Tenant,
            "Generator", InventoryCategory.Equipment, null, null, "unit", null, null, Bought, 1_000, 1m, null, TestData.UserId, null,
            TestData.Today, TestData.Now)).Errors.Keys);
        Assert.Throws<ValidationFailedException>(() => InventoryItem.Register(Guid.NewGuid(), TestData.Tenant, "Generator",
            InventoryCategory.Equipment, null, "G-1", "unit", null, null, Bought, 1_000, 2m, null, TestData.UserId, null, TestData.Today, TestData.Now));

        var excavator = NewExcavator();
        Assert.Equal("PC200812345", excavator.NormalizedSerialNumber);
        Assert.Throws<DomainRuleException>(() => excavator.Receive(1m, Later, null, TestData.UserId, null, TestData.Today, TestData.Now));
        Assert.Throws<ValidationFailedException>(() => excavator.Transfer(StockLocation.Warehouse, StockLocation.Project(Guid.NewGuid()), 2m, Later,
            false, null, TestData.UserId, null, TestData.Today, TestData.Now));
    }

    [Fact]
    public void A_machine_is_in_exactly_one_place_and_a_written_off_machine_is_retired_and_can_be_restored_by_reversal()
    {
        var excavator = NewExcavator();
        var site = StockLocation.Project(Guid.NewGuid());
        excavator.Transfer(StockLocation.Warehouse, site, 1m, Later, false, null, TestData.UserId, null, TestData.Today, TestData.Now);

        Assert.Equal(1m, excavator.BalanceAt(site));
        Assert.Equal(0m, excavator.BalanceAt(StockLocation.Warehouse));

        var writeOff = excavator.WriteOff(site, 1m, "Written off after the accident", Later, TestData.UserId, null, TestData.Today, TestData.Now);
        Assert.Equal(InventoryItemStatus.Retired, excavator.Status);
        Assert.Throws<DomainRuleException>(() => excavator.Transfer(site, StockLocation.Warehouse, 1m, Later, false, null, TestData.UserId, null, TestData.Today, TestData.Now));

        excavator.Reverse(writeOff, "Insurance repaired it", Later, TestData.UserId, TestData.Today, TestData.Now);
        Assert.Equal(InventoryItemStatus.Active, excavator.Status);
        Assert.Equal(1m, excavator.BalanceAt(site));
    }

    [Fact]
    public void An_item_with_stock_can_not_be_retired_and_one_without_stock_can()
    {
        var item = NewTools(quantity: 2m);
        Assert.Throws<DomainRuleException>(() => item.Retire("Obsolete", TestData.Now));

        item.WriteOff(StockLocation.Warehouse, 2m, "Disposed", Later, TestData.UserId, null, TestData.Today, TestData.Now);
        Assert.Throws<ValidationFailedException>(() => item.Retire(" ", TestData.Now));
        item.Retire("Obsolete", TestData.Now);

        Assert.Equal(InventoryItemStatus.Retired, item.Status);
        Assert.Throws<DomainRuleException>(() => item.Receive(1m, Later, null, TestData.UserId, null, TestData.Today, TestData.Now));
    }

    [Fact]
    public void Item_details_are_validated_together()
    {
        var ex = Assert.Throws<ValidationFailedException>(() => InventoryItem.Register(Guid.NewGuid(), TestData.Tenant, " ", InventoryCategory.Tool, null,
            null, " ", null, null, default, -1, 1m, null, TestData.UserId, null, TestData.Today, TestData.Now));

        foreach (var field in new[] { "description", "unit", "unitCostMinor", "purchaseDate" }) Assert.Contains(field, ex.Errors.Keys);
    }

    [Fact]
    public void Location_keys_round_trip()
    {
        var project = Guid.NewGuid();
        Assert.Equal(StockLocation.Warehouse, StockLocation.FromKey("W"));
        Assert.Equal(StockLocation.Project(project), StockLocation.FromKey(StockLocation.Project(project).Key));
        Assert.Throws<ArgumentException>(() => StockLocation.FromKey("X"));
    }
}
