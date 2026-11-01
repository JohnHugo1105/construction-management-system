using System.Globalization;
using Cms.Application.Common;
using Cms.Application.Features.Costing;
using Cms.Application.Features.Organizations;
using Cms.Application.Features.Projects;
using Cms.Domain.Common;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Inventory;
using Cms.Domain.Features.Organizations;
using Cms.Domain.Features.Projects;

namespace Cms.Application.Features.Inventory;

/// <summary>
/// Warehouse use cases: register tools, equipment and machinery, and move, count, write off and reverse their stock. Every stock change
/// is one ledger line plus the new balances, saved together with its audit line (and, for a charged transfer, the project cost), or not
/// at all. Two people moving the same last units can't both succeed: the item's version makes the second save a conflict.
/// </summary>
public sealed class InventoryService(IInventoryRepository inventory, IInventoryReader reader, IProjectRepository projects,
    IOrganizationRepository organizations, ICostEntryRepository costs, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork,
    IAuditTrail audit)
{
    private const string WarehouseName = "Warehouse";

    public async Task<PagedResult<InventoryListItemDto>> ListAsync(InventoryListRequest request, PageRequest page, CancellationToken cancellationToken)
    {
        var errors = new ValidationErrors();
        InventoryCategory? category = null;
        InventoryItemStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            if (Enum.TryParse<InventoryCategory>(request.Category, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)) category = parsed;
            else errors.Add("category", "Choose tool, equipment or machinery.");
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (Enum.TryParse<InventoryItemStatus>(request.Status, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)) status = parsed;
            else errors.Add("status", "Choose active or retired.");
        }

        errors.ThrowIfAny();
        return await reader.ListAsync(new InventoryListQuery(request.Search, category, status, request.ProjectId, request.InWarehouse ?? false), page,
            cancellationToken);
    }

    public Task<InventoryItemDto> GetAsync(Guid id, CancellationToken cancellationToken) => ReadAsync(id, cancellationToken);

    public async Task<PagedResult<StockMovementDto>> ListMovementsAsync(Guid itemId, PageRequest page, CancellationToken cancellationToken) =>
        await reader.ListMovementsAsync(itemId, page, cancellationToken) ?? throw new NotFoundException("that item");

    public async Task<PagedResult<ProjectStockDto>> ListForProjectAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken) =>
        await reader.ListForProjectAsync(projectId, page, cancellationToken) ?? throw new NotFoundException("that project");

    public async Task<InventoryItemDto> RegisterAsync(RegisterItemRequest request, CancellationToken cancellationToken)
    {
        var requestId = Clean(request.ClientRequestId);
        if (requestId is not null && await inventory.FindItemIdByRequestIdAsync(requestId, cancellationToken) is { } existing)
            return await ReadAsync(existing, cancellationToken);

        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var now = clock.UtcNow;
        // A category the domain doesn't know is reported by the domain with the other field problems, in one response.
        var category = Enum.TryParse<InventoryCategory>(request.Category, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : (InventoryCategory)(-1);
        var item = InventoryItem.Register(Guid.NewGuid(), currentUser.TenantId, request.Description, category, request.Brand, request.SerialNumber,
            request.Unit, request.SourceDealer, request.InvoiceNumber, request.PurchaseDate, request.UnitCostMinor, request.Quantity, request.Remarks,
            currentUser.UserId, requestId, organization.TodayLocal(now), now);
        if (await SerialIsTakenAsync(item, null, cancellationToken))
        {
            // A double-click: the first copy of this very form may have been saved since the check above, and it owns this serial number.
            if (requestId is not null && await inventory.FindItemIdByRequestIdAsync(requestId, cancellationToken) is { } saved)
                return await ReadAsync(saved, cancellationToken);
            throw SerialTaken(item);
        }

        inventory.Add(item);
        audit.Record("Inventory.Registered", nameof(InventoryItem), item.Id,
            $"Registered {Describe(item)} and received {Qty(request.Quantity)} {item.Unit} into the {WarehouseName.ToLowerInvariant()}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(item.Id, cancellationToken);
    }

    public Task<InventoryItemDto> UpdateAsync(Guid id, UpdateItemRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, request.Version, null, async step =>
        {
            var item = step.Item;
            var before = item.Description;
            item.UpdateDetails(request.Description, request.Brand, request.SerialNumber, request.Unit, request.SourceDealer, request.InvoiceNumber,
                request.PurchaseDate, request.UnitCostMinor, request.Remarks, step.Today, step.Now);
            await EnsureSerialIsFreeAsync(item, item.Id, cancellationToken);
            return new Outcome("Inventory.Updated", before == item.Description
                ? $"Updated {Describe(item)}."
                : $"Updated {Short(before, 60)}, now named {Describe(item)}.");
        }, cancellationToken);

    public Task<InventoryItemDto> ReceiveAsync(Guid id, ReceiveStockRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, request.Version, request.ClientRequestId, step =>
        {
            var movement = step.Item.Receive(request.Quantity, request.Date ?? step.Today, request.Note, currentUser.UserId, step.RequestId, step.Today,
                step.Now);
            return Task.FromResult(new Outcome("Inventory.Received",
                $"Received {Qty(movement.Quantity)} {step.Item.Unit} of {Describe(step.Item)} into the {WarehouseName.ToLowerInvariant()}."));
        }, cancellationToken);

    public Task<InventoryItemDto> TransferAsync(Guid id, TransferStockRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, request.Version, request.ClientRequestId, async step =>
        {
            var errors = new ValidationErrors();
            var from = ParseLocation(request.FromLocation, "fromLocation", errors);
            var to = ParseLocation(request.ToLocation, "toLocation", errors);
            errors.ThrowIfAny();

            var places = new Places(projects);
            await places.LoadAsync(from, "fromLocation", cancellationToken);
            var destination = await places.LoadAsync(to, "toLocation", cancellationToken);
            if (destination is not null) EnsureCanReceive(destination);

            var item = step.Item;
            var movement = item.Transfer(from!.Value, to!.Value, request.Quantity, request.Date ?? step.Today, request.ChargeToProject, request.Note,
                currentUser.UserId, step.RequestId, step.Today, step.Now);
            var summary = $"Moved {Qty(movement.Quantity)} {item.Unit} of {Describe(item)} from {places.NameOf(from)} to {places.NameOf(to)}.";

            if (request.ChargeToProject)
            {
                // A charge that rounds to nothing must not silently skip the cost entry the person asked for.
                if (movement.ChargedAmountMinor <= 0)
                    throw new ValidationFailedException("chargeToProject",
                        "The charge works out to nothing at this quantity and unit cost. Leave the charge off, or check the item's unit cost.");

                // The cost is created in the same save as the stock move: both exist or neither does.
                costs.Add(CostEntry.RecordSystem(Guid.NewGuid(), currentUser.TenantId, destination!, null, CostOrigin.Inventory, movement.Id,
                    ChargeDescription(item, movement.Quantity, places.NameOf(from)), ItemType.Equipment, 1m, "l.s", movement.ChargedAmountMinor,
                    VatTreatment.Inclusive, step.Organization.VatRateBps, movement.MovementDate, currentUser.UserId, step.Now));
                summary += $" Charged {Money(movement.ChargedAmountMinor, step.Organization.CurrencyCode)} to the project.";
            }

            return new Outcome("Inventory.Transferred", summary);
        }, cancellationToken);

    public Task<InventoryItemDto> AdjustAsync(Guid id, AdjustStockRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, request.Version, request.ClientRequestId, async step =>
        {
            var location = ParseSingleLocation(request.Location);
            var places = new Places(projects);
            await places.LoadAsync(location, "location", cancellationToken);

            var item = step.Item;
            var recorded = item.BalanceAt(location);
            item.Adjust(location, request.CountedQuantity, request.Reason, request.Date ?? step.Today, currentUser.UserId, step.RequestId, step.Today,
                step.Now);
            return new Outcome("Inventory.Adjusted",
                $"Stock count of {Describe(item)} at {places.NameOf(location)}: counted {Qty(request.CountedQuantity)} {item.Unit}, the records said {Qty(recorded)}. " +
                Because(request.Reason));
        }, cancellationToken);

    public Task<InventoryItemDto> WriteOffAsync(Guid id, WriteOffStockRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, request.Version, request.ClientRequestId, async step =>
        {
            var location = ParseSingleLocation(request.Location);
            var places = new Places(projects);
            await places.LoadAsync(location, "location", cancellationToken);

            var item = step.Item;
            var movement = item.WriteOff(location, request.Quantity, request.Reason, request.Date ?? step.Today, currentUser.UserId, step.RequestId,
                step.Today, step.Now);
            return new Outcome("Inventory.WrittenOff",
                $"Wrote off {Qty(movement.Quantity)} {item.Unit} of {Describe(item)} at {places.NameOf(location)}. {Because(movement.Note)}" +
                (item.Status == InventoryItemStatus.Retired ? " The item is now retired." : ""));
        }, cancellationToken);

    public Task<InventoryItemDto> ReverseAsync(Guid id, Guid movementId, ReverseMovementRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, request.Version, request.ClientRequestId, async step =>
        {
            var original = await inventory.GetMovementAsync(id, movementId, cancellationToken) ?? throw new NotFoundException("that stock movement");
            var places = new Places(projects);
            await places.LoadAsync(original.From, "movement", cancellationToken);
            await places.LoadAsync(original.To, "movement", cancellationToken);
            // The reversal puts the stock back where it came from, so that place must still accept it.
            if (original.From is { IsWarehouse: false } returnsTo) EnsureCanReceive(places.Require(returnsTo));
            // Reversing a charged transfer voids a cost on the project it was charged to; an archived project is read-only, so restore it first.
            if (original.ChargedAmountMinor > 0 && original.To is { IsWarehouse: false } chargedTo && places.Require(chargedTo).IsArchived)
                throw new DomainRuleException($"{places.Require(chargedTo).Name} is archived. Restore it before reversing this transfer, so the finished project's figures don't change unnoticed.");

            var item = step.Item;
            var reversal = item.Reverse(original, request.Reason, step.Today, currentUser.UserId, step.Today, step.Now, step.RequestId);

            // A charged transfer put a cost on the project. Reversing the transfer voids it in the same save, with the same reason.
            var voided = 0L;
            if (original.ChargedAmountMinor > 0)
            {
                foreach (var entry in await costs.ListByOriginAsync(CostOrigin.Inventory, original.Id, cancellationToken))
                {
                    if (entry.IsVoided) continue;
                    entry.Void(currentUser.UserId, reversal.Note, step.Now);
                    voided += entry.GrossMinor;
                }
            }

            var summary = $"Reversed {WhatWasDone(original, places)} ({Qty(original.Quantity)} {item.Unit} of {Describe(item)}). {Because(reversal.Note)}";
            if (voided > 0) summary += $" The {Money(voided, step.Organization.CurrencyCode)} charge to the project was voided.";
            return new Outcome("Inventory.Reversed", summary);
        }, cancellationToken);

    public Task<InventoryItemDto> RetireAsync(Guid id, RetireItemRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(id, request.Version, null, step =>
        {
            step.Item.Retire(request.Reason, step.Now);
            return Task.FromResult(new Outcome("Inventory.Retired",
                $"Retired {Describe(step.Item)}. {Because(step.Item.RetiredReason)}"));
        }, cancellationToken);

    /// <summary>
    /// The path every change to an existing item takes. A repeated request id returns the original result before anything else (a
    /// repeat carries the old version, so it must not be judged as a stale edit). Otherwise: load, check the version the person
    /// opened, change the item, audit, and save once.
    /// </summary>
    private async Task<InventoryItemDto> ChangeAsync(Guid itemId, int version, string? clientRequestId, Func<Step, Task<Outcome>> change,
        CancellationToken cancellationToken)
    {
        var requestId = Clean(clientRequestId);
        if (requestId is not null && await inventory.FindItemIdByRequestIdAsync(requestId, cancellationToken) is { } doneFor)
        {
            if (doneFor != itemId) throw new DomainRuleException("That request was already used for a different item. Reload the page and try again.");
            return await ReadAsync(itemId, cancellationToken);
        }

        var item = await inventory.GetAsync(itemId, cancellationToken) ?? throw new NotFoundException("that item");
        ConcurrencyGuard.Ensure(item.Version, version);
        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var now = clock.UtcNow;

        var outcome = await change(new Step(item, organization, organization.TodayLocal(now), now, requestId));
        audit.Record(outcome.Action, nameof(InventoryItem), item.Id, outcome.Summary);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadAsync(item.Id, cancellationToken);
    }

    private async Task<InventoryItemDto> ReadAsync(Guid id, CancellationToken cancellationToken) =>
        await reader.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that item");

    private async Task EnsureSerialIsFreeAsync(InventoryItem item, Guid? excludeItemId, CancellationToken cancellationToken)
    {
        if (await SerialIsTakenAsync(item, excludeItemId, cancellationToken)) throw SerialTaken(item);
    }

    private async Task<bool> SerialIsTakenAsync(InventoryItem item, Guid? excludeItemId, CancellationToken cancellationToken) =>
        item.NormalizedSerialNumber is { } serial && await inventory.SerialExistsAsync(serial, excludeItemId, cancellationToken);

    private static ValidationFailedException SerialTaken(InventoryItem item) => new("serialNumber",
        $"Serial number \"{item.SerialNumber}\" is already registered to another item. Check the number.");

    private static StockLocation? ParseLocation(StockLocationRequest? request, string field, ValidationErrors errors)
    {
        if (request is null || !Enum.TryParse<StockLocationKind>(request.Kind, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            errors.Add(field, "Choose the warehouse or a project.");
            return null;
        }

        if (kind == StockLocationKind.Warehouse) return StockLocation.Warehouse;
        if (request.ProjectId is not { } projectId || projectId == Guid.Empty)
        {
            errors.Add(field, "Choose the project.");
            return null;
        }

        return StockLocation.Project(projectId);
    }

    private static StockLocation ParseSingleLocation(StockLocationRequest? request)
    {
        var errors = new ValidationErrors();
        var location = ParseLocation(request, "location", errors);
        errors.ThrowIfAny();
        return location!.Value;
    }

    private static void EnsureCanReceive(Project project)
    {
        if (project.IsArchived)
            throw new DomainRuleException($"The project \"{project.Name}\" is archived. Restore it before moving stock there.");
        project.EnsureCanReceiveRecordsFor(null);
    }

    private static string WhatWasDone(StockMovement original, Places places) => original.Type switch
    {
        MovementType.Purchase => "the receipt into the warehouse",
        MovementType.Transfer => $"the move from {places.NameOf(original.From)} to {places.NameOf(original.To)}",
        MovementType.Adjustment => "a stock count adjustment",
        MovementType.WriteOff => $"the write-off at {places.NameOf(original.From)}",
        _ => "a movement"
    };

    private static string ChargeDescription(InventoryItem item, decimal quantity, string source)
    {
        const string prefix = "Equipment charged: ";
        var suffix = $" x {Qty(quantity)} (from {source})";
        var room = Math.Max(10, 200 - prefix.Length - suffix.Length);
        return Short(prefix + Short(item.Description, room) + suffix, 200);
    }

    private static string Describe(InventoryItem item) => item.IsUnitTracked && item.SerialNumber is not null
        ? $"{Short(item.Description, 60)} (serial {Short(item.SerialNumber, 30)})"
        : Short(item.Description, 60);

    private static string Because(string? reason) => $"Reason: {Short((reason ?? "").Trim().TrimEnd('.'), 100)}.";

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string Qty(decimal quantity) => quantity.ToString("0.####", CultureInfo.InvariantCulture);

    private static string Money(long minor, string currencyCode) => $"{currencyCode} {(minor / 100m).ToString("N2", CultureInfo.InvariantCulture)}";

    /// <summary>Keeps a name short enough that an audit summary or a cost description can't outgrow its column.</summary>
    private static string Short(string text, int maximum) => text.Length <= maximum ? text : text[..(maximum - 1)].TrimEnd() + "…";

    private sealed record Step(InventoryItem Item, Organization Organization, DateOnly Today, DateTimeOffset Now, string? RequestId);

    private sealed record Outcome(string Action, string Summary);

    /// <summary>The projects one request names, loaded once, so a place can be checked and called by its name.</summary>
    private sealed class Places(IProjectRepository projects)
    {
        private readonly Dictionary<Guid, Project> _known = [];

        /// <summary>Loads the project behind a location (null for the warehouse). A project outside the company is a field error.</summary>
        public async Task<Project?> LoadAsync(StockLocation? location, string field, CancellationToken cancellationToken)
        {
            if (location?.ProjectId is not { } id) return null;
            if (_known.TryGetValue(id, out var known)) return known;
            var project = await projects.GetAsync(id, cancellationToken) ??
                throw new ValidationFailedException(field, "That project isn't one of your company's projects. Choose a project from the list.");
            _known[id] = project;
            return project;
        }

        public Project Require(StockLocation location) => _known[location.ProjectId!.Value];

        public string NameOf(StockLocation? location) => location switch
        {
            null => "outside the system",
            { IsWarehouse: true } => WarehouseName,
            { ProjectId: { } id } when _known.TryGetValue(id, out var project) => Short(project.Name, 60),
            _ => "a project"
        };
    }
}
