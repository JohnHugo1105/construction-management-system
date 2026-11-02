using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.SiteDiary;
using ConstructOPX.Application.Features.Workforce;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.Domain.Features.Payroll;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.Purchasing;
using ConstructOPX.Domain.Features.SiteDiary;
using ConstructOPX.Domain.Features.Suppliers;
using ConstructOPX.Domain.Features.Workforce;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.SiteDiary;

internal sealed class SiteDiaryRepository(ConstructOpxDbContext db) : ISiteDiaryRepository
{
    public Task<SiteDiaryEntry?> GetLiveAsync(Guid projectId, DateOnly reportDate, CancellationToken cancellationToken) =>
        db.Set<SiteDiaryEntry>().SingleOrDefaultAsync(item => item.ProjectId == projectId && item.ReportDate == reportDate && item.Status == DiaryStatus.Posted,
            cancellationToken);

    public Task<bool> LiveExistsAsync(Guid projectId, DateOnly reportDate, CancellationToken cancellationToken) =>
        db.Set<SiteDiaryEntry>().AnyAsync(item => item.ProjectId == projectId && item.ReportDate == reportDate && item.Status == DiaryStatus.Posted,
            cancellationToken);

    public Task<DiaryRequestOwner?> FindByClientRequestAsync(string clientRequestId, CancellationToken cancellationToken) =>
        db.Set<SiteDiaryEntry>().AsNoTracking().Where(item => item.ClientRequestId == clientRequestId)
            .Select(item => new DiaryRequestOwner(item.Id, item.ProjectId, item.ReportDate)).SingleOrDefaultAsync(cancellationToken);

    public void Add(SiteDiaryEntry entry) => db.Set<SiteDiaryEntry>().Add(entry);
}

/// <summary>
/// Reads reports straight into DTOs. A project's list is two statements (a count and a page, with each day's headcount counted in the page
/// statement), one report is three or four (the report, the workers' count, their names only for a caller allowed to see them, and the
/// deliveries), however many reports, workers or deliveries exist. The workers' names are read only for a caller who holds
/// <c>workforce.read</c>; no pay figure and no price is ever selected, so even a mapping mistake could not put one on the wire.
/// </summary>
internal sealed class SiteDiaryReader(ConstructOpxDbContext db) : ISiteDiaryReader
{
    /// <summary>The most worker names and delivery lines one report returns; the worker count is always the true one.</summary>
    private const int NamesShown = 300;

    private const int DeliveryLinesShown = 200;

    public async Task<PagedResult<DiaryListItemDto>> ListAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken)
    {
        var rows = db.Set<SiteDiaryEntry>().AsNoTracking().Where(item => item.ProjectId == projectId && item.Status == DiaryStatus.Posted);
        var total = await rows.CountAsync(cancellationToken);
        var users = db.Set<User>();
        var attendance = db.Set<AttendanceEntry>();
        var items = await rows.OrderByDescending(item => item.ReportDate).ThenBy(item => item.Id).Skip(page.Skip).Take(page.SafePageSize)
            .Select(item => new
            {
                item.Id, item.ReportDate, item.Weather, item.WorkDone, item.SubcontractorWorkers, item.Version,
                Workers = attendance.Where(entry => entry.ProjectId == item.ProjectId && entry.WorkDate == item.ReportDate &&
                    entry.Status == AttendanceStatus.Recorded).Select(entry => entry.WorkerId).Distinct().Count(),
                Author = users.Where(user => user.Id == item.AuthorUserId).Select(user => user.DisplayName).FirstOrDefault()
            }).ToListAsync(cancellationToken);
        return PagedResult<DiaryListItemDto>.Of(items.Select(row => new DiaryListItemDto(row.Id, row.ReportDate, row.Weather.ToString(), row.Workers,
            row.SubcontractorWorkers, DiaryPreview.FirstLine(row.WorkDone), row.Author, row.Version)).ToArray(), total, page);
    }

    public Task<DiaryEntryDto?> GetAsync(Guid projectId, DateOnly reportDate, DiaryReadScope scope, CancellationToken cancellationToken) =>
        ReadAsync(db.Set<SiteDiaryEntry>().Where(item => item.ProjectId == projectId && item.ReportDate == reportDate && item.Status == DiaryStatus.Posted),
            scope, cancellationToken);

    public Task<DiaryEntryDto?> GetByIdAsync(Guid id, DiaryReadScope scope, CancellationToken cancellationToken) =>
        ReadAsync(db.Set<SiteDiaryEntry>().Where(item => item.Id == id), scope, cancellationToken);

    public async Task<DiaryFilledInDto> FilledInAsync(Guid projectId, DateOnly reportDate, bool includeNames, CancellationToken cancellationToken)
    {
        // Workers with attendance recorded that day on this project, counted once each. Hours, rates and pay are never selected.
        var workerIds = db.Set<AttendanceEntry>().AsNoTracking()
            .Where(entry => entry.ProjectId == projectId && entry.WorkDate == reportDate && entry.Status == AttendanceStatus.Recorded)
            .Select(entry => entry.WorkerId).Distinct();
        var count = await workerIds.CountAsync(cancellationToken);

        IReadOnlyList<string>? names = null;
        if (includeNames)
        {
            var rows = await db.Set<Worker>().AsNoTracking().Where(worker => workerIds.Contains(worker.Id))
                .OrderBy(worker => worker.LastName).ThenBy(worker => worker.FirstName).ThenBy(worker => worker.Id).Take(NamesShown)
                .Select(worker => new { worker.FirstName, worker.MiddleName, worker.LastName }).ToListAsync(cancellationToken);
            names = rows.Select(row => WorkerNames.Full(row.FirstName, row.MiddleName, row.LastName)).ToArray();
        }

        // What posted deliveries received that day on this project's orders: the firm, the item as ordered, its unit and the quantity. Only
        // those four columns are selected: never a unit cost, VAT or amount (the same rule as the receiving screen).
        var delivered = await (
            from line in db.Set<GoodsReceiptLine>().AsNoTracking()
            join receipt in db.Set<GoodsReceipt>() on line.GoodsReceiptId equals receipt.Id
            where receipt.ProjectId == projectId && receipt.ReceivedDate == reportDate && receipt.Status == GoodsReceiptStatus.Posted
            join orderLine in db.Set<PurchaseOrderLine>() on line.PurchaseOrderLineId equals orderLine.Id
            join supplier in db.Set<Supplier>() on receipt.SupplierId equals supplier.Id
            select new { SupplierName = supplier.Name, orderLine.Description, orderLine.Unit, line.Quantity })
            .GroupBy(row => new { row.SupplierName, row.Description, row.Unit })
            .Select(group => new { group.Key.SupplierName, group.Key.Description, group.Key.Unit, Quantity = group.Sum(row => row.Quantity) })
            .OrderBy(row => row.SupplierName).ThenBy(row => row.Description).ThenBy(row => row.Unit).Take(DeliveryLinesShown)
            .ToListAsync(cancellationToken);

        return new DiaryFilledInDto(new DiaryOnSiteDto(count, names),
            delivered.Select(row => new DiaryDeliveryDto(row.SupplierName, row.Description, row.Unit, row.Quantity)).ToArray());
    }

    private async Task<DiaryEntryDto?> ReadAsync(IQueryable<SiteDiaryEntry> source, DiaryReadScope scope, CancellationToken cancellationToken)
    {
        var users = db.Set<User>();
        var head = await (
            from entry in source.AsNoTracking()
            join project in db.Set<Project>() on entry.ProjectId equals project.Id
            select new
            {
                Entry = entry,
                ProjectName = project.Name,
                ProjectArchived = project.Status == ProjectStatus.Archived,
                Author = users.Where(user => user.Id == entry.AuthorUserId).Select(user => user.DisplayName).FirstOrDefault()
            }).SingleOrDefaultAsync(cancellationToken);
        if (head is null) return null;

        var e = head.Entry;
        var filled = await FilledInAsync(e.ProjectId, e.ReportDate, scope.CanSeeWorkerNames, cancellationToken);
        var canChange = scope.CanWrite && !head.ProjectArchived && e.CanBeChangedBy(scope.UserId, scope.CanManage, scope.Today);
        return new DiaryEntryDto(e.Id, e.ProjectId, head.ProjectName, head.ProjectArchived, e.ReportDate, e.Weather.ToString(), e.WeatherNote, e.WorkDone,
            e.IssuesAndDelays, e.VisitorsAndInspections, e.SafetyNotes, e.SubcontractorWorkers, e.Status.ToString(), head.Author, e.CreatedAtUtc, e.UpdatedAtUtc,
            SiteDiaryEntry.AuthorWindowEnds(e.ReportDate), canChange, filled.OnSite, filled.Deliveries, e.Version);
    }
}
