using Cms.Application.Common;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Projects;

namespace Cms.Application.Features.Costing;

// ---- Ports ------------------------------------------------------------------------------------------------------------------

public interface IBudgetLineRepository
{
    /// <summary>Loads a budget line tracked for change. Null when it doesn't exist in the organization.</summary>
    Task<BudgetLine?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The line an earlier request with this id created, so a repeated request returns it instead of making a second one.</summary>
    Task<BudgetLine?> FindByClientRequestIdAsync(string clientRequestId, CancellationToken cancellationToken);

    void Add(BudgetLine line);
}

/// <summary>Reads budget lines straight into DTOs in SQL. Never hydrates whole aggregates to build a list.</summary>
public interface IBudgetLineReader
{
    /// <summary>One page of the filtered lines, plus the money totals of the whole filtered set (removed lines never count in them).</summary>
    Task<TotaledPage<BudgetLineDto>> ListAsync(Guid projectId, BudgetLineFilter filter, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Every filtered line in list order, for exports. Rows are streamed from the database, not loaded all at once.</summary>
    IAsyncEnumerable<BudgetLineDto> StreamAsync(Guid projectId, BudgetLineFilter filter, CancellationToken cancellationToken);
}

/// <summary>Reads cost entries straight into DTOs in SQL.</summary>
public interface ICostEntryReader
{
    /// <summary>One page of the filtered entries, plus the money totals of the whole filtered set (voided entries never count in them).</summary>
    Task<TotaledPage<CostEntryDto>> ListAsync(Guid projectId, CostEntryFilter filter, PageRequest page, CancellationToken cancellationToken);

    IAsyncEnumerable<CostEntryDto> StreamAsync(Guid projectId, CostEntryFilter filter, CancellationToken cancellationToken);
}

// ---- Shared shapes ----------------------------------------------------------------------------------------------------------

public sealed record MoneyTotalsDto(long NetMinor, long VatMinor, long GrossMinor)
{
    public static MoneyTotalsDto Zero { get; } = new(0, 0, 0);
}

/// <summary>The usual paged shape plus the money totals of the whole filtered set, not only of this page.</summary>
public sealed record TotaledPage<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize, MoneyTotalsDto Totals)
{
    public static TotaledPage<T> Of(IReadOnlyList<T> items, int total, PageRequest request, MoneyTotalsDto totals) =>
        new(items, total, request.SafePage, request.SafePageSize, totals);
}

/// <summary>A downloadable file produced by an export.</summary>
public sealed record ExportFile(string FileName, string ContentType, byte[] Content);

// ---- Budget lines -----------------------------------------------------------------------------------------------------------

/// <summary>The filters as the person typed them. The service checks them and turns them into a <see cref="BudgetLineFilter"/>.</summary>
public sealed record BudgetLineListQuery(Guid? SubProjectId, int? Division, string? ItemType, string? Search, bool IncludeRemoved);

public sealed record BudgetLineFilter(Guid? SubProjectId, int? Division, ItemType? ItemType, string? Search, bool IncludeRemoved);

public sealed record BudgetLineDto(Guid Id, Guid ProjectId, Guid? SubProjectId, string? SubProjectName, int? Division, string? MainScope,
    string? SubScope, string? WbsCode, string Description, string ItemType, decimal Quantity, string Unit, long UnitCostMinor,
    string VatTreatment, int VatRateBps, long NetMinor, long VatMinor, long GrossMinor, DateOnly EntryDate, string? Remarks, string Status,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? RemovedAtUtc, string? RemovedReason, int Version, Guid? ChangeOrderId = null,
    string? ChangeOrderNumber = null);

/// <summary>
/// The VAT rate is not a field: the company's rate at the moment of entry is stored on the line. <c>ItemType</c> and <c>VatTreatment</c> are
/// names (for example "Material", "Inclusive"); see the reference data endpoint for the choices.
/// </summary>
public sealed record CreateBudgetLineRequest(Guid? SubProjectId, int? Division, string? MainScope, string? SubScope, string? WbsCode,
    string? Description, string? ItemType, decimal Quantity, string? Unit, long UnitCostMinor, string? VatTreatment, DateOnly EntryDate,
    string? Remarks, string? ClientRequestId);

public sealed record UpdateBudgetLineRequest(Guid? SubProjectId, int? Division, string? MainScope, string? SubScope, string? WbsCode,
    string? Description, string? ItemType, decimal Quantity, string? Unit, long UnitCostMinor, string? VatTreatment, DateOnly EntryDate,
    string? Remarks, int Version);

public sealed record RemoveBudgetLineRequest(string? Reason, int Version);

// ---- Cost entries -----------------------------------------------------------------------------------------------------------

public sealed record CostEntryListQuery(Guid? SubProjectId, int? Division, string? ItemType, string? Origin, DateOnly? From, DateOnly? To,
    string? Search, bool IncludeVoided);

public sealed record CostEntryFilter(Guid? SubProjectId, int? Division, ItemType? ItemType, CostOrigin? Origin, DateOnly? From, DateOnly? To,
    string? Search, bool IncludeVoided);

/// <param name="CanEdit">True for a manual entry that is still posted. System-made costs are corrected where they came from.</param>
public sealed record CostEntryDto(Guid Id, Guid ProjectId, Guid? SubProjectId, string? SubProjectName, int? Division, string? MainScope,
    string? SubScope, string? WbsCode, string Description, string ItemType, decimal Quantity, string Unit, long UnitCostMinor,
    string VatTreatment, int VatRateBps, long NetMinor, long VatMinor, long GrossMinor, DateOnly EntryDate, string? Remarks, string Origin,
    Guid? OriginReferenceId, string SourceDealer, string? InvoiceNumber, string Status, DateTimeOffset CreatedAtUtc,
    DateTimeOffset? VoidedAtUtc, string? VoidReason, bool CanEdit, int Version);

public sealed record CreateCostEntryRequest(Guid? SubProjectId, int? Division, string? MainScope, string? SubScope, string? WbsCode,
    string? Description, string? ItemType, decimal Quantity, string? Unit, long UnitCostMinor, string? VatTreatment, DateOnly EntryDate,
    string? Remarks, string? SourceDealer, string? InvoiceNumber, string? ClientRequestId);

public sealed record UpdateCostEntryRequest(Guid? SubProjectId, int? Division, string? MainScope, string? SubScope, string? WbsCode,
    string? Description, string? ItemType, decimal Quantity, string? Unit, long UnitCostMinor, string? VatTreatment, DateOnly EntryDate,
    string? Remarks, string? SourceDealer, string? InvoiceNumber, int Version);

public sealed record VoidCostEntryRequest(string? Reason, int Version);

// ---- Reference data ---------------------------------------------------------------------------------------------------------

public sealed record WbsScopeDto(string Code, string Name);

public sealed record WbsDivisionDto(int Number, string MainScope, IReadOnlyList<WbsScopeDto> Scopes);

public sealed record ChoiceDto(string Value, string Label);

public sealed record VatChoiceDto(string Value, string Label, string Hint);

public sealed record CostingReferenceDto(IReadOnlyList<WbsDivisionDto> Divisions, IReadOnlyList<string> Units, IReadOnlyList<ChoiceDto> ItemTypes,
    IReadOnlyList<VatChoiceDto> VatTreatments, int VatRateBps);

// ---- Mapping ----------------------------------------------------------------------------------------------------------------

public static class CostingMapper
{
    /// <param name="changeOrderNumber">"CO-001" when the line was added by a change order.</param>
    public static BudgetLineDto ToDto(BudgetLine line, Project project, string? changeOrderNumber = null) => new(line.Id, line.ProjectId,
        line.SubProjectId, SubProjectName(project, line.SubProjectId), line.Division, line.MainScope, line.SubScope, line.WbsCode, line.Description,
        line.ItemType.ToString(), line.Quantity, line.Unit, line.UnitCostMinor, line.VatTreatment.ToString(), line.VatRateBps, line.NetMinor,
        line.VatMinor, line.GrossMinor, line.EntryDate, line.Remarks, line.Status.ToString(), line.CreatedAtUtc, line.RemovedAtUtc,
        line.RemovedReason, line.Version, line.ChangeOrderId, changeOrderNumber);

    public static CostEntryDto ToDto(CostEntry entry, Project project) => new(entry.Id, entry.ProjectId, entry.SubProjectId,
        SubProjectName(project, entry.SubProjectId), entry.Division, entry.MainScope, entry.SubScope, entry.WbsCode, entry.Description,
        entry.ItemType.ToString(), entry.Quantity, entry.Unit, entry.UnitCostMinor, entry.VatTreatment.ToString(), entry.VatRateBps,
        entry.NetMinor, entry.VatMinor, entry.GrossMinor, entry.EntryDate, entry.Remarks, entry.Origin.ToString(), entry.OriginReferenceId,
        entry.SourceDealer, entry.InvoiceNumber, entry.Status.ToString(), entry.CreatedAtUtc, entry.VoidedAtUtc, entry.VoidReason,
        entry.Origin == CostOrigin.Manual && !entry.IsVoided, entry.Version);

    private static string? SubProjectName(Project project, Guid? subProjectId) =>
        subProjectId is null ? null : project.SubProjects.FirstOrDefault(sub => sub.Id == subProjectId)?.Name;
}
