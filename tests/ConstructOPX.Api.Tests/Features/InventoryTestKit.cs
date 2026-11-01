using System.Net;
using System.Text.Json;
using ConstructOPX.Api.Tests.Infrastructure;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Audit;
using ConstructOPX.Application.Features.Inventory;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.Domain.Features.Inventory;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ConstructOPX.Api.Tests.Features;

/// <summary>
/// One company with a signed-in person, plus the few calls every inventory test needs. Each test starts its own world, so none
/// depends on another and a company never sees another's data by accident.
/// </summary>
internal sealed class InventoryWorld : IDisposable
{
    public const string ItemsUrl = "/api/inventory/items";

    /// <summary>A purchase date safely in the past in every time zone.</summary>
    public static readonly DateOnly Bought = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30);

    private readonly List<ApiClient> _others = [];

    private InventoryWorld(ConstructOpxApiFactory factory, ApiClient client, SeededUser user)
    {
        Factory = factory;
        Client = client;
        User = user;
    }

    public ConstructOpxApiFactory Factory { get; }
    public ApiClient Client { get; }
    public SeededUser User { get; }

    public static async Task<InventoryWorld> StartAsync(ConstructOpxApiFactory factory, AccessRole roles = AccessRole.Administrator)
    {
        var user = await TenantSeeder.CreateTenantAsync(factory, roles);
        var client = new ApiClient(factory);
        await client.SignInAsync(user.Username, user.Password);
        return new InventoryWorld(factory, client, user);
    }

    /// <summary>Another signed-in person in the same company.</summary>
    public async Task<ApiClient> ColleagueAsync(AccessRole roles)
    {
        var user = await TenantSeeder.AddUserAsync(Factory, User.TenantId, roles);
        var client = new ApiClient(Factory);
        await client.SignInAsync(user.Username, user.Password);
        _others.Add(client);
        return client;
    }

    public void Dispose()
    {
        Client.Dispose();
        foreach (var other in _others) other.Dispose();
    }

    public static string Url(Guid itemId, string action = "") => action.Length == 0 ? $"{ItemsUrl}/{itemId}" : $"{ItemsUrl}/{itemId}/{action}";

    public static string Day(DateOnly date) => date.ToString("yyyy-MM-dd");

    // ----- request bodies -----

    public static object Warehouse => new { kind = "Warehouse" };

    public static object At(Guid projectId) => new { kind = "Project", projectId };

    public static object ToolBody(string description = "Claw hammer", decimal quantity = 10m, long unitCostMinor = 45_000, string unit = "pcs",
        string? brand = "Stanley", string? requestId = null) =>
        new
        {
            description, category = "Tool", brand, serialNumber = (string?)null, unit, sourceDealer = "Hardware Depot", invoiceNumber = "INV-1",
            purchaseDate = Bought, unitCostMinor, quantity, remarks = (string?)null, clientRequestId = requestId
        };

    public static object MachineBody(string description = "Excavator PC200", string serial = "PC200-8-12345", long unitCostMinor = 250_000_000,
        string? requestId = null) =>
        new
        {
            description, category = "Machinery", brand = "Komatsu", serialNumber = serial, unit = "unit", sourceDealer = (string?)null,
            invoiceNumber = (string?)null, purchaseDate = Bought, unitCostMinor, quantity = 1m, remarks = (string?)null, clientRequestId = requestId
        };

    public static object UpdateBody(InventoryItemDto item, string? description = null, string? brand = null, string? serial = null, string? unit = null,
        long? unitCostMinor = null, int? version = null) =>
        new
        {
            description = description ?? item.Description, brand = brand ?? item.Brand, serialNumber = serial ?? item.SerialNumber,
            unit = unit ?? item.Unit, sourceDealer = item.SourceDealer, invoiceNumber = item.InvoiceNumber, purchaseDate = item.PurchaseDate,
            unitCostMinor = unitCostMinor ?? item.UnitCostMinor, remarks = item.Remarks, version = version ?? item.Version
        };

    public static object Move(object from, object to, decimal quantity, int version, bool charge = false, string? note = null, string? requestId = null,
        DateOnly? date = null) =>
        new { fromLocation = from, toLocation = to, quantity, date, chargeToProject = charge, note, clientRequestId = requestId, version };

    public static object Receive(decimal quantity, int version, DateOnly? date = null, string? requestId = null) =>
        new { quantity, date, note = (string?)null, clientRequestId = requestId, version };

    public static object Count(object location, decimal counted, string? reason, int version, string? requestId = null) =>
        new { location, countedQuantity = counted, reason, date = (DateOnly?)null, clientRequestId = requestId, version };

    public static object WriteOff(object location, decimal quantity, string? reason, int version, string? requestId = null) =>
        new { location, quantity, reason, date = (DateOnly?)null, clientRequestId = requestId, version };

    public static object Reverse(string? reason, int version, string? requestId = null) => new { reason, clientRequestId = requestId, version };

    public static object Retire(string? reason, int version) => new { reason, version };

    // ----- calls -----

    public Task<ProjectDto> ProjectAsync(string name) =>
        Client.PostAsync<ProjectDto>("/api/projects",
            new { name, client = "ACME Realty", location = "Cebu City", contractPriceMinor = 5_000_000_00L, startDate = "2026-01-05", targetEndDate = (string?)null });

    public Task<ProjectDto> ArchiveAsync(ProjectDto project) =>
        Client.PostAsync<ProjectDto>($"/api/projects/{project.Id}/archive", new { reason = "Finished", note = (string?)null, version = project.Version });

    public Task<InventoryItemDto> RegisterToolAsync(string description = "Claw hammer", decimal quantity = 10m, long unitCostMinor = 45_000) =>
        Client.PostAsync<InventoryItemDto>(ItemsUrl, ToolBody(description, quantity, unitCostMinor));

    public Task<InventoryItemDto> RegisterMachineAsync(string description = "Excavator PC200", string serial = "PC200-8-12345") =>
        Client.PostAsync<InventoryItemDto>(ItemsUrl, MachineBody(description, serial));

    public Task<InventoryItemDto> GetAsync(Guid id) => Client.GetAsync<InventoryItemDto>(Url(id));

    /// <summary>Moves stock and returns the item as the API now reports it.</summary>
    public Task<InventoryItemDto> TransferAsync(InventoryItemDto item, object from, object to, decimal quantity, bool charge = false, DateOnly? date = null) =>
        Client.PostAsync<InventoryItemDto>(Url(item.Id, "transfer"), Move(from, to, quantity, item.Version, charge, null, null, date));

    /// <summary>The ledger, newest first.</summary>
    public async Task<List<StockMovementDto>> LedgerAsync(Guid itemId) =>
        (await Client.GetAsync<PagedResult<StockMovementDto>>($"{Url(itemId, "movements")}?pageSize=100")).Items.ToList();

    public async Task<List<CostEntry>> CostEntriesAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConstructOpxDbContext>();
        return await db.Set<CostEntry>().IgnoreQueryFilters().AsNoTracking().Where(entry => entry.TenantId == User.TenantId)
            .OrderBy(entry => entry.CreatedAtUtc).ToListAsync();
    }

    public async Task<List<AuditEntryDto>> AuditAsync(Guid itemId) =>
        (await Client.GetAsync<PagedResult<AuditEntryDto>>($"/api/audit?entityType=InventoryItem&entityId={itemId}&pageSize=100")).Items.ToList();

    /// <summary>Adds items straight to the database (a fast way to get volume; the API paths are covered by their own tests).</summary>
    public async Task SeedItemsAsync(Guid projectId, int count, string prefix)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConstructOpxDbContext>();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var index = 0; index < count; index++)
        {
            var item = InventoryItem.Register(Guid.NewGuid(), User.TenantId, $"{prefix} {index:D3}", InventoryCategory.Tool, "Brand", null, "pc", null, null,
                Bought, 1_000, 5m, null, User.UserId, null, today, now);
            item.Transfer(StockLocation.Warehouse, StockLocation.Project(projectId), 2m, today, false, null, User.UserId, null, today, now);
            db.Set<InventoryItem>().Add(item);
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Adds ledger lines to one item straight to the database by moving a piece back and forth.</summary>
    public async Task SeedMovementsAsync(Guid itemId, Guid projectId, int pairs)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConstructOpxDbContext>();
        var item = await db.Set<InventoryItem>().IgnoreQueryFilters().Include(i => i.Balances).SingleAsync(i => i.Id == itemId);
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var index = 0; index < pairs; index++)
        {
            item.Transfer(StockLocation.Warehouse, StockLocation.Project(projectId), 1m, today, false, null, User.UserId, null, today, now.AddSeconds(index * 2 + 1));
            item.Transfer(StockLocation.Project(projectId), StockLocation.Warehouse, 1m, today, false, null, User.UserId, null, today, now.AddSeconds(index * 2 + 2));
        }

        await db.SaveChangesAsync();
    }

    // ----- assertions -----

    /// <summary>Asserts the status and (optionally) the problem code, and returns the problem body.</summary>
    public static async Task<JsonElement> ExpectAsync(HttpResponseMessage response, HttpStatusCode status, string? code = null)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Expected {(int)status} but got {(int)response.StatusCode}: {text}");
        var problem = JsonDocument.Parse(text).RootElement.Clone();
        if (code is not null) Assert.Equal(code, problem.GetProperty("code").GetString());
        return problem;
    }

    /// <summary>Asserts a 422 and returns the names of the fields that carry an error.</summary>
    public static async Task<string[]> InvalidFieldsAsync(HttpResponseMessage response)
    {
        var problem = await ExpectAsync(response, HttpStatusCode.UnprocessableEntity, "validation_failed");
        return problem.GetProperty("errors").EnumerateObject().Select(property => property.Name).ToArray();
    }

    public static async Task<string> RuleDetailAsync(HttpResponseMessage response, string code = "rule_violation") =>
        (await ExpectAsync(response, HttpStatusCode.Conflict, code)).GetProperty("detail").GetString()!;

    public static decimal QuantityAt(InventoryItemDto item, string location, Guid? projectId = null) =>
        item.Balances.Where(balance => balance.Location == location && balance.ProjectId == projectId).Sum(balance => balance.Quantity);
}
