using Cms.Application.Common;
using Cms.Domain.Features.Identity;
using Cms.Domain.Features.Inventory;
using Cms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cms.Api.Tests.Features;

/// <summary>
/// The database-level promises of the inventory mapping, read from the real EF model (no database needed): the unique indexes that back
/// the service's checks, the stock quantity precision, and the append-only ledger's keys.
/// </summary>
public sealed class InventoryModelTests
{
    private static readonly Lazy<IModel> Model = new(() =>
    {
        var options = new DbContextOptionsBuilder<CmsDbContext>().UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options;
        using var db = new CmsDbContext(options, new NoOneSignedIn());
        return db.Model;
    });

    private static IIndex IndexOn<T>(params string[] columns) =>
        Model.Value.FindEntityType(typeof(T))!.GetIndexes().Single(index => index.Properties.Select(property => property.Name).SequenceEqual(columns));

    [Fact]
    public void A_serial_number_is_unique_within_a_company_when_there_is_one()
    {
        var index = IndexOn<InventoryItem>("TenantId", "NormalizedSerialNumber");

        Assert.True(index.IsUnique);
        Assert.Equal("[NormalizedSerialNumber] IS NOT NULL", index.GetFilter());
    }

    [Fact]
    public void An_item_has_one_balance_row_per_place()
    {
        Assert.True(IndexOn<StockBalance>("ItemId", "LocationKey").IsUnique);
    }

    [Fact]
    public void A_request_id_is_unique_within_a_company_when_there_is_one_so_a_double_click_cannot_record_twice()
    {
        var index = IndexOn<StockMovement>("TenantId", "ClientRequestId");

        Assert.True(index.IsUnique);
        Assert.Equal("[ClientRequestId] IS NOT NULL", index.GetFilter());
    }

    [Fact]
    public void The_queries_the_screens_run_have_an_index()
    {
        Assert.NotNull(IndexOn<StockMovement>("ItemId", "RecordedAtUtc"));
        Assert.NotNull(IndexOn<StockBalance>("TenantId", "ProjectId"));
        Assert.NotNull(IndexOn<InventoryItem>("TenantId", "Description"));
        Assert.NotNull(IndexOn<InventoryItem>("TenantId", "Status"));
    }

    [Fact]
    public void Stock_quantities_keep_four_decimals_and_notes_and_keys_have_a_length()
    {
        foreach (var (type, property) in new[] { (typeof(StockBalance), "Quantity"), (typeof(StockMovement), "Quantity") })
        {
            var column = Model.Value.FindEntityType(type)!.FindProperty(property)!;
            Assert.Equal((18, 4), (column.GetPrecision(), column.GetScale()));
        }

        var movement = Model.Value.FindEntityType(typeof(StockMovement))!;
        Assert.Equal(300, movement.FindProperty(nameof(StockMovement.Note))!.GetMaxLength());
        Assert.Equal(80, movement.FindProperty(nameof(StockMovement.ClientRequestId))!.GetMaxLength());
        Assert.All(new[] { nameof(StockMovement.FromKey), nameof(StockMovement.ToKey) }, key => Assert.NotNull(movement.FindProperty(key)!.GetMaxLength()));
        Assert.NotNull(Model.Value.FindEntityType(typeof(StockBalance))!.FindProperty(nameof(StockBalance.LocationKey))!.GetMaxLength());
    }

    [Fact]
    public void Removing_a_balance_row_deletes_it_but_the_ledger_can_never_be_removed_with_its_item()
    {
        var item = Model.Value.FindEntityType(typeof(InventoryItem))!;

        Assert.Equal(DeleteBehavior.Cascade, item.GetNavigations().Single(navigation => navigation.Name == nameof(InventoryItem.Balances)).ForeignKey.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, item.GetNavigations().Single(navigation => navigation.Name == nameof(InventoryItem.Movements)).ForeignKey.DeleteBehavior);
    }

    private sealed class NoOneSignedIn : ICurrentUser
    {
        public bool IsAuthenticated => false;
        public Guid UserId => Guid.Empty;
        public Guid TenantId => Guid.Empty;
        public string UserName => "";
        public string DisplayName => "";
        public AccessRole Roles => AccessRole.None;
        public string? CorrelationId => null;
    }
}
