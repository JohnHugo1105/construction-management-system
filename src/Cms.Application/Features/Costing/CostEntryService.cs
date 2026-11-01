using System.Globalization;
using Cms.Application.Common;
using Cms.Application.Features.Organizations;
using Cms.Application.Features.Projects;
using Cms.Domain.Common;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Projects;

namespace Cms.Application.Features.Costing;

/// <summary>
/// Cost entry use cases: what was actually spent (the desktop app's "journal"). A person records a supplier invoice or receipt; payroll and
/// stock transfers post their own entries through <see cref="ICostEntryRepository"/>. An entry is voided with a reason, never deleted.
/// The same supplier invoice can be recorded only once per project: the retired app let it in again and again.
/// </summary>
public sealed class CostEntryService(ICostEntryRepository entries, ICostEntryReader reader, IProjectRepository projects,
    IOrganizationRepository organizations, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IAuditTrail audit)
{
    public async Task<TotaledPage<CostEntryDto>> ListAsync(Guid projectId, CostEntryListQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        // The project is looked up first so a project of another company answers 404, not an empty list.
        await LoadProject(projectId, cancellationToken);
        return await reader.ListAsync(projectId, CostingInput.ToFilter(query), page, cancellationToken);
    }

    public async Task<CostEntryDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = await Load(id, cancellationToken);
        return CostingMapper.ToDto(entry, await LoadProject(entry.ProjectId, cancellationToken));
    }

    public async Task<CostEntryDto> CreateAsync(Guid projectId, CreateCostEntryRequest request, CancellationToken cancellationToken)
    {
        var project = await LoadProject(projectId, cancellationToken);

        // A repeated request (double-click, retry) returns the entry it made the first time.
        var requestId = CostingInput.RequestId(request.ClientRequestId);
        if (requestId is not null && await entries.FindByClientRequestIdAsync(requestId, cancellationToken) is { } original)
        {
            if (original.ProjectId != project.Id)
                throw new DomainRuleException("That request was already used for another project. Reload the page and try again.");
            return CostingMapper.ToDto(original, project);
        }

        var organization = await organizations.GetCurrentAsync(cancellationToken);
        // "Today" is the date in the company's business time zone, never the UTC day: a cost entered at 7 a.m. in Manila is dated today.
        var today = organization.TodayLocal(clock.UtcNow);
        var entry = CostEntry.RecordManual(Guid.NewGuid(), currentUser.TenantId, project, request.SubProjectId, request.Division, request.MainScope,
            request.SubScope, request.WbsCode, request.Description, CostingInput.ParseItemType(request.ItemType), request.Quantity, request.Unit,
            request.UnitCostMinor, CostingInput.ParseVatTreatment(request.VatTreatment), organization.VatRateBps, request.EntryDate, request.Remarks,
            request.SourceDealer, request.InvoiceNumber, currentUser.UserId, requestId, today, clock.UtcNow);
        await EnsureInvoiceIsNewAsync(entry, cancellationToken);

        entries.Add(entry);
        audit.Record("CostEntry.Recorded", nameof(CostEntry), entry.Id,
            $"Recorded cost \"{entry.Description}\" ({CostingInput.MoneyText(entry.GrossMinor, organization.CurrencyCode)} with VAT) from " +
            $"{entry.SourceDealer}, invoice {entry.InvoiceNumber}, for {project.Name}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CostingMapper.ToDto(entry, project);
    }

    public async Task<CostEntryDto> UpdateAsync(Guid id, UpdateCostEntryRequest request, CancellationToken cancellationToken)
    {
        var entry = await Load(id, cancellationToken);
        var project = await LoadProject(entry.ProjectId, cancellationToken);
        ConcurrencyGuard.Ensure(entry.Version, request.Version);

        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var today = organization.TodayLocal(clock.UtcNow);
        var grossBefore = entry.GrossMinor;
        // The entry keeps the VAT rate it was recorded with.
        entry.Revise(project, request.SubProjectId, request.Division, request.MainScope, request.SubScope, request.WbsCode, request.Description,
            CostingInput.ParseItemType(request.ItemType), request.Quantity, request.Unit, request.UnitCostMinor,
            CostingInput.ParseVatTreatment(request.VatTreatment), entry.VatRateBps, request.EntryDate, request.Remarks, request.SourceDealer,
            request.InvoiceNumber, today, clock.UtcNow);
        await EnsureInvoiceIsNewAsync(entry, cancellationToken);

        audit.Record("CostEntry.Updated", nameof(CostEntry), entry.Id, grossBefore == entry.GrossMinor
            ? $"Updated cost \"{entry.Description}\" of {project.Name}."
            : $"Updated cost \"{entry.Description}\" of {project.Name}, total with VAT from " +
              $"{CostingInput.MoneyText(grossBefore, organization.CurrencyCode)} to {CostingInput.MoneyText(entry.GrossMinor, organization.CurrencyCode)}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CostingMapper.ToDto(entry, project);
    }

    /// <summary>
    /// Voids an entry with a reason. A cost made by payroll or by a stock transfer is normally voided by the feature that owns it; voiding
    /// one here is allowed, but the audit line says it was voided at the entry, so the source and the project no longer agree.
    /// </summary>
    public async Task<CostEntryDto> VoidAsync(Guid id, VoidCostEntryRequest request, CancellationToken cancellationToken)
    {
        var entry = await Load(id, cancellationToken);
        var project = await LoadProject(entry.ProjectId, cancellationToken);
        ConcurrencyGuard.Ensure(entry.Version, request.Version);
        // An archived project is read-only: restore it first, so a finished project's figures don't change unnoticed.
        project.EnsureNotArchived();

        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var origin = entry.Origin;
        entry.Void(currentUser.UserId, request.Reason, clock.UtcNow);

        var amount = CostingInput.MoneyText(entry.GrossMinor, organization.CurrencyCode);
        if (origin == CostOrigin.Manual)
            audit.Record("CostEntry.Voided", nameof(CostEntry), entry.Id,
                $"Voided cost \"{entry.Description}\" ({amount} with VAT) from {entry.SourceDealer}, invoice {entry.InvoiceNumber}, of {project.Name}. Reason: {entry.VoidReason}");
        else
            audit.Record("CostEntry.VoidedAtEntry", nameof(CostEntry), entry.Id,
                $"Voided at the entry itself, not at its source ({CostingLabels.OriginLabel(origin)}): cost \"{entry.Description}\" ({amount} with VAT) " +
                $"of {project.Name}. Reason: {entry.VoidReason}");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CostingMapper.ToDto(entry, project);
    }

    /// <summary>The filtered entries as a CSV file, in the same order as the list.</summary>
    public async Task<ExportFile> ExportAsync(Guid projectId, CostEntryListQuery query, CancellationToken cancellationToken)
    {
        var project = await LoadProject(projectId, cancellationToken);
        var filter = CostingInput.ToFilter(query);
        var organization = await organizations.GetCurrentAsync(cancellationToken);

        var csv = new CsvExport.Document("Date", "Supplier", "Invoice number", "Source", "Division", "Main scope", "Sub-scope", "WBS code", "Sub-project",
            "Description", "Item type", "Quantity", "Unit", "Unit cost", "VAT treatment", "VAT rate %", "Net (before VAT)", "VAT", "Gross (with VAT)",
            "Remarks", "Status");
        await foreach (var entry in reader.StreamAsync(projectId, filter, cancellationToken).WithCancellation(cancellationToken))
        {
            csv.Row(CsvExport.Date(entry.EntryDate), CsvExport.Text(entry.SourceDealer), CsvExport.Text(entry.InvoiceNumber),
                CostingLabels.OriginLabel(Enum.Parse<CostOrigin>(entry.Origin)), entry.Division?.ToString(CultureInfo.InvariantCulture) ?? "",
                CsvExport.Text(entry.MainScope), CsvExport.Text(entry.SubScope), CsvExport.Text(entry.WbsCode), CsvExport.Text(entry.SubProjectName),
                CsvExport.Text(entry.Description), CostingLabels.ItemTypeLabel(Enum.Parse<ItemType>(entry.ItemType)), CsvExport.Quantity(entry.Quantity),
                CsvExport.Text(entry.Unit), CsvExport.Money(entry.UnitCostMinor),
                CostingLabels.VatTreatmentLabel(Enum.Parse<VatTreatment>(entry.VatTreatment)), CsvExport.Percent(entry.VatRateBps),
                CsvExport.Money(entry.NetMinor), CsvExport.Money(entry.VatMinor), CsvExport.Money(entry.GrossMinor), CsvExport.Text(entry.Remarks),
                entry.Status);
        }

        return new ExportFile(CsvExport.FileName(project.Name, "costs", organization.TodayLocal(clock.UtcNow)), CsvExport.ContentType, csv.ToArray());
    }

    /// <summary>
    /// Refuses a supplier invoice that is already recorded for the project (same supplier and invoice number once case, spaces and
    /// punctuation are ignored). The filtered unique index is the backstop when two people save the same invoice at the same moment.
    /// </summary>
    private async Task EnsureInvoiceIsNewAsync(CostEntry entry, CancellationToken cancellationToken)
    {
        if (entry.NormalizedInvoiceNumber is not { } invoice) return;
        var duplicate = await entries.FindDuplicateInvoiceAsync(entry.ProjectId, entry.NormalizedSourceDealer, invoice, entry.Id, cancellationToken);
        if (duplicate is not null)
            throw new ValidationFailedException("invoiceNumber",
                $"This supplier invoice is already recorded for this project, dated {CostingInput.DateText(duplicate.EntryDate)}. " +
                "Check the invoice number, or void the earlier entry first if it was a mistake.");
    }

    private async Task<CostEntry> Load(Guid id, CancellationToken cancellationToken) =>
        await entries.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that cost entry");

    private async Task<Project> LoadProject(Guid id, CancellationToken cancellationToken) =>
        await projects.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that project");
}
