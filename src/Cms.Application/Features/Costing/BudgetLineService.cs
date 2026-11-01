using System.Globalization;
using Cms.Application.Common;
using Cms.Application.Features.Organizations;
using Cms.Application.Features.Projects;
using Cms.Domain.Common;
using Cms.Domain.Features.Costing;
using Cms.Domain.Features.Projects;

namespace Cms.Application.Features.Costing;

/// <summary>
/// Budget line use cases. A line is the planned cost of an item (the desktop app's "bill of materials"). Its money is worked out once by
/// the domain from the quantity, the unit cost, the VAT treatment and the company's VAT rate at the moment of entry, and is stored; a
/// later change of the company rate never touches an existing line. A line is removed with a reason, never deleted.
/// </summary>
public sealed class BudgetLineService(IBudgetLineRepository lines, IBudgetLineReader reader, IProjectRepository projects,
    IOrganizationRepository organizations, ICurrentUser currentUser, IClock clock, IUnitOfWork unitOfWork, IAuditTrail audit)
{
    public async Task<TotaledPage<BudgetLineDto>> ListAsync(Guid projectId, BudgetLineListQuery query, PageRequest page,
        CancellationToken cancellationToken)
    {
        // The project is looked up first so a project of another company answers 404, not an empty list.
        await LoadProject(projectId, cancellationToken);
        return await reader.ListAsync(projectId, CostingInput.ToFilter(query), page, cancellationToken);
    }

    public async Task<BudgetLineDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var line = await Load(id, cancellationToken);
        return CostingMapper.ToDto(line, await LoadProject(line.ProjectId, cancellationToken));
    }

    public async Task<BudgetLineDto> CreateAsync(Guid projectId, CreateBudgetLineRequest request, CancellationToken cancellationToken)
    {
        var project = await LoadProject(projectId, cancellationToken);

        // A repeated request (double-click, retry) returns the line it made the first time.
        var requestId = CostingInput.RequestId(request.ClientRequestId);
        if (requestId is not null && await lines.FindByClientRequestIdAsync(requestId, cancellationToken) is { } original)
        {
            if (original.ProjectId != project.Id)
                throw new DomainRuleException("That request was already used for another project. Reload the page and try again.");
            return CostingMapper.ToDto(original, project);
        }

        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var line = BudgetLine.Create(Guid.NewGuid(), currentUser.TenantId, project, request.SubProjectId, request.Division, request.MainScope,
            request.SubScope, request.WbsCode, request.Description, CostingInput.ParseItemType(request.ItemType), request.Quantity, request.Unit,
            request.UnitCostMinor, CostingInput.ParseVatTreatment(request.VatTreatment), organization.VatRateBps, request.EntryDate, request.Remarks,
            currentUser.UserId, requestId, clock.UtcNow);

        lines.Add(line);
        audit.Record("BudgetLine.Created", nameof(BudgetLine), line.Id,
            $"Added budget line \"{line.Description}\" ({CostingInput.MoneyText(line.GrossMinor, organization.CurrencyCode)} with VAT) to {project.Name}.");
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DomainRuleException) when (requestId is not null)
        {
            // The same form submitted twice at once: the first was saved between this one's look-up and its save, and the database refused
            // the second. Answer it with the line the first made, as a repeat that arrived a moment later would be.
            if (await lines.FindByClientRequestIdAsync(requestId, cancellationToken) is { } first && first.ProjectId == project.Id)
                return CostingMapper.ToDto(first, project);
            throw;
        }
        return CostingMapper.ToDto(line, project);
    }

    public async Task<BudgetLineDto> UpdateAsync(Guid id, UpdateBudgetLineRequest request, CancellationToken cancellationToken)
    {
        var line = await Load(id, cancellationToken);
        var project = await LoadProject(line.ProjectId, cancellationToken);
        ConcurrencyGuard.Ensure(line.Version, request.Version);

        var organization = await organizations.GetCurrentAsync(cancellationToken);
        var grossBefore = line.GrossMinor;
        // The line keeps the VAT rate it was entered with, so editing the quantity of an old line doesn't silently reprice its VAT.
        line.Revise(project, request.SubProjectId, request.Division, request.MainScope, request.SubScope, request.WbsCode, request.Description,
            CostingInput.ParseItemType(request.ItemType), request.Quantity, request.Unit, request.UnitCostMinor,
            CostingInput.ParseVatTreatment(request.VatTreatment), line.VatRateBps, request.EntryDate, request.Remarks, clock.UtcNow);

        audit.Record("BudgetLine.Updated", nameof(BudgetLine), line.Id, grossBefore == line.GrossMinor
            ? $"Updated budget line \"{line.Description}\" of {project.Name}."
            : $"Updated budget line \"{line.Description}\" of {project.Name}, total with VAT from " +
              $"{CostingInput.MoneyText(grossBefore, organization.CurrencyCode)} to {CostingInput.MoneyText(line.GrossMinor, organization.CurrencyCode)}.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CostingMapper.ToDto(line, project);
    }

    public async Task<BudgetLineDto> RemoveAsync(Guid id, RemoveBudgetLineRequest request, CancellationToken cancellationToken)
    {
        var line = await Load(id, cancellationToken);
        var project = await LoadProject(line.ProjectId, cancellationToken);
        ConcurrencyGuard.Ensure(line.Version, request.Version);
        // An archived project is read-only: restore it first, so a finished project's figures don't change unnoticed.
        project.EnsureNotArchived();

        var organization = await organizations.GetCurrentAsync(cancellationToken);
        line.Remove(currentUser.UserId, request.Reason, clock.UtcNow);
        audit.Record("BudgetLine.Removed", nameof(BudgetLine), line.Id,
            $"Removed budget line \"{line.Description}\" ({CostingInput.MoneyText(line.GrossMinor, organization.CurrencyCode)} with VAT) from {project.Name}. Reason: {line.RemovedReason}");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CostingMapper.ToDto(line, project);
    }

    /// <summary>The filtered lines as a CSV file, in the same order as the list.</summary>
    public async Task<ExportFile> ExportAsync(Guid projectId, BudgetLineListQuery query, CancellationToken cancellationToken)
    {
        var project = await LoadProject(projectId, cancellationToken);
        var filter = CostingInput.ToFilter(query);
        var organization = await organizations.GetCurrentAsync(cancellationToken);

        var csv = new CsvExport.Document("Date", "Division", "Main scope", "Sub-scope", "WBS code", "Sub-project", "Description", "Item type",
            "Quantity", "Unit", "Unit cost", "VAT treatment", "VAT rate %", "Net (before VAT)", "VAT", "Gross (with VAT)", "Remarks", "Status");
        await foreach (var line in reader.StreamAsync(projectId, filter, cancellationToken).WithCancellation(cancellationToken))
        {
            csv.Row(CsvExport.Date(line.EntryDate), line.Division?.ToString(CultureInfo.InvariantCulture) ?? "", CsvExport.Text(line.MainScope),
                CsvExport.Text(line.SubScope), CsvExport.Text(line.WbsCode), CsvExport.Text(line.SubProjectName), CsvExport.Text(line.Description),
                CostingLabels.ItemTypeLabel(Enum.Parse<ItemType>(line.ItemType)), CsvExport.Quantity(line.Quantity), CsvExport.Text(line.Unit),
                CsvExport.Money(line.UnitCostMinor), CostingLabels.VatTreatmentLabel(Enum.Parse<VatTreatment>(line.VatTreatment)),
                CsvExport.Percent(line.VatRateBps), CsvExport.Money(line.NetMinor), CsvExport.Money(line.VatMinor), CsvExport.Money(line.GrossMinor),
                CsvExport.Text(line.Remarks), line.Status);
        }

        return new ExportFile(CsvExport.FileName(project.Name, "budget", organization.TodayLocal(clock.UtcNow)), CsvExport.ContentType, csv.ToArray());
    }

    private async Task<BudgetLine> Load(Guid id, CancellationToken cancellationToken) =>
        await lines.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that budget line");

    private async Task<Project> LoadProject(Guid id, CancellationToken cancellationToken) =>
        await projects.GetAsync(id, cancellationToken) ?? throw new NotFoundException("that project");
}
