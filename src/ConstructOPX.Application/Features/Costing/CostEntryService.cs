using System.Globalization;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Organizations;
using ConstructOPX.Application.Features.Projects;
using ConstructOPX.Application.Features.Purchasing;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Costing;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.Purchasing;

namespace ConstructOPX.Application.Features.Costing;

/// <summary>
/// Cost entry use cases: what was actually spent (the desktop app's "journal"). A person records a supplier invoice or receipt; payroll and
/// stock transfers post their own entries through <see cref="ICostEntryRepository"/>. An entry is voided with a reason, never deleted.
/// The same supplier invoice can be recorded only once per project: the retired app let it in again and again.
/// </summary>
public sealed class CostEntryService(ICostEntryRepository entries, ICostEntryReader reader, IProjectRepository projects,
    IOrganizationRepository organizations, IPurchaseOrderLineLookup purchaseOrderLines, IPurchaseOrderRepository purchaseOrders, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork,
    IAuditTrail audit)
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
        var project = await LoadProject(entry.ProjectId, cancellationToken);
        return CostingMapper.ToDto(entry, project, await OrderNumberAsync(entry.PurchaseOrderLineId, cancellationToken));
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
            return CostingMapper.ToDto(original, project, await OrderNumberAsync(original.PurchaseOrderLineId, cancellationToken));
        }

        var organization = await organizations.GetCurrentAsync(cancellationToken);
        // "Today" is the date in the company's business time zone, never the UTC day: a cost entered at 7 a.m. in Manila is dated today.
        var today = organization.TodayLocal(clock.UtcNow);
        var entry = CostEntry.RecordManual(Guid.NewGuid(), currentUser.TenantId, project, request.SubProjectId, request.Division, request.MainScope,
            request.SubScope, request.WbsCode, request.Description, CostingInput.ParseItemType(request.ItemType), request.Quantity, request.Unit,
            request.UnitCostMinor, CostingInput.ParseVatTreatment(request.VatTreatment), organization.VatRateBps, request.EntryDate, request.Remarks,
            request.SourceDealer, request.InvoiceNumber, currentUser.UserId, requestId, today, clock.UtcNow);
        await EnsureInvoiceIsNewAsync(entry, cancellationToken);
        var order = await MatchedLineAsync(project.Id, request.PurchaseOrderLineId, currentLineId: null, cancellationToken);
        entry.LinkToPurchaseOrderLine(order?.LineId);

        entries.Add(entry);
        audit.Record("CostEntry.Recorded", nameof(CostEntry), entry.Id,
            $"Recorded cost \"{entry.Description}\" ({CostingInput.MoneyText(entry.GrossMinor, organization.CurrencyCode)} with VAT) from " +
            $"{entry.SourceDealer}, invoice {entry.InvoiceNumber}, for {project.Name}" + (order is null ? "." : $", matched to purchase order {order.OrderNumber}."));
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DomainRuleException) when (requestId is not null)
        {
            // The same form submitted twice at once: the first was saved between this one's look-up and its save, and the database refused
            // the second. Answer it with the entry the first made. A genuine duplicate invoice has no earlier entry with this request id,
            // so it is reported as the refusal it is.
            if (await entries.FindByClientRequestIdAsync(requestId, cancellationToken) is { } first && first.ProjectId == project.Id)
                return CostingMapper.ToDto(first, project, await OrderNumberAsync(first.PurchaseOrderLineId, cancellationToken));
            throw;
        }
        return CostingMapper.ToDto(entry, project, order?.OrderNumber);
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
        // Keeping the match the entry already has is always fine (its order may have been closed since); a new match must be to an approved order.
        var order = await MatchedLineAsync(project.Id, request.PurchaseOrderLineId, entry.PurchaseOrderLineId, cancellationToken);
        entry.LinkToPurchaseOrderLine(request.PurchaseOrderLineId);

        audit.Record("CostEntry.Updated", nameof(CostEntry), entry.Id, grossBefore == entry.GrossMinor
            ? $"Updated cost \"{entry.Description}\" of {project.Name}."
            : $"Updated cost \"{entry.Description}\" of {project.Name}, total with VAT from " +
              $"{CostingInput.MoneyText(grossBefore, organization.CurrencyCode)} to {CostingInput.MoneyText(entry.GrossMinor, organization.CurrencyCode)}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CostingMapper.ToDto(entry, project, order?.OrderNumber);
    }

    /// <summary>
    /// Voids a hand-entered cost with a reason. A cost made by payroll or by a stock transfer is never voided here: it is reversed by the
    /// feature that owns it (void the payroll run, reverse the transfer), which keeps the source and the project agreeing. Allowing it
    /// here would let someone with cost access quietly remove labor cost that payroll still says was paid.
    /// </summary>
    public async Task<CostEntryDto> VoidAsync(Guid id, VoidCostEntryRequest request, CancellationToken cancellationToken)
    {
        var entry = await Load(id, cancellationToken);
        var project = await LoadProject(entry.ProjectId, cancellationToken);
        ConcurrencyGuard.Ensure(entry.Version, request.Version);
        if (entry.Origin != CostOrigin.Manual)
            throw new DomainRuleException(entry.Origin switch
            {
                CostOrigin.Payroll => "This cost was made by payroll. Void the payroll run instead, so the pay and the cost stay in step.",
                CostOrigin.Subcontract => "This cost was made by a subcontractor payment certificate. Void the certificate instead, so what is owed and the cost stay in step.",
                _ => "This cost was made by a stock transfer. Reverse the transfer instead, so the stock and the cost stay in step."
            });
        // An archived project is read-only: restore it first, so a finished project's figures don't change unnoticed.
        project.EnsureNotArchived();

        var organization = await organizations.GetCurrentAsync(cancellationToken);
        entry.Void(currentUser.UserId, request.Reason, clock.UtcNow);

        var amount = CostingInput.MoneyText(entry.GrossMinor, organization.CurrencyCode);
        audit.Record("CostEntry.Voided", nameof(CostEntry), entry.Id,
            $"Voided cost \"{entry.Description}\" ({amount} with VAT) from {entry.SourceDealer}, invoice {entry.InvoiceNumber}, of {project.Name}. Reason: {entry.VoidReason}");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CostingMapper.ToDto(entry, project, await OrderNumberAsync(entry.PurchaseOrderLineId, cancellationToken));
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

    /// <summary>
    /// The purchase order line an invoice is matched to, checked: it must exist in this company, belong to an order of the same project, and
    /// that order must be approved. A match the entry already has (<paramref name="currentLineId"/>) is not checked again. Null for no match.
    /// </summary>
    private async Task<PurchaseOrderLineRef?> MatchedLineAsync(Guid projectId, Guid? lineId, Guid? currentLineId, CancellationToken cancellationToken)
    {
        if (lineId is null) return null;
        var line = await purchaseOrderLines.FindAsync(lineId.Value, cancellationToken) ??
            throw new ValidationFailedException("purchaseOrderLineId", "That purchase order line wasn't found. Choose one from the list, or leave it empty.");
        if (lineId != currentLineId)
        {
            PurchaseOrderMatching.EnsureLineCanBeMatched(projectId, line.ProjectId, line.OrderStatus, line.OrderNumber);
            // Read the order again as tracked and touch it: a cancel or close racing this invoice then fails on the order's version
            // instead of leaving a cancelled order with a matched invoice.
            var order = await purchaseOrders.GetAsync(line.OrderId, cancellationToken) ?? throw new NotFoundException("that purchase order");
            order.NoteMatchedInvoice(clock.UtcNow);
        }
        return line;
    }

    private async Task<string?> OrderNumberAsync(Guid? lineId, CancellationToken cancellationToken) =>
        lineId is null ? null : (await purchaseOrderLines.FindAsync(lineId.Value, cancellationToken))?.OrderNumber;

    private async Task<CostEntry> Load(Guid id, CancellationToken cancellationToken) =>
        await entries.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that cost entry");

    private async Task<Project> LoadProject(Guid id, CancellationToken cancellationToken) =>
        await projects.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that project");
}
