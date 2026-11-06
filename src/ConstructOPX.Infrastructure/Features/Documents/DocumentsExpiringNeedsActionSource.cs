using System.Globalization;
using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.NeedsAction;
using ConstructOPX.Domain.Features.Documents;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.Documents;

/// <summary>
/// The "Needs your action" group <c>documents-expiring</c>: Active documents on projects that are not archived whose valid-until date has
/// passed or comes within 30 days (the bond rule), for people who manage documents. Expired ones first, then the soonest. One statement for
/// the count and one for the rows. A row never carries an amount; Contract and Insurance documents are left out for anyone who may not read
/// costs (every role that manages documents may, today).
/// </summary>
internal sealed class DocumentsExpiringNeedsActionSource(ConstructOpxDbContext db, ICurrentUser currentUser) : INeedsActionSource
{
    public const string GroupKey = "documents-expiring";

    public string Key => GroupKey;

    public bool CanActOn(AccessRole roles) => Permissions.IsAllowed(roles, Permissions.DocumentsManage);

    public Task<int> CountAsync(NeedsActionDates dates, CancellationToken cancellationToken) => Expiring(dates).CountAsync(cancellationToken);

    public async Task<NeedsActionGroupDto> ReadAsync(NeedsActionDates dates, int take, CancellationToken cancellationToken)
    {
        var count = await CountAsync(dates, cancellationToken);
        if (count == 0) return new NeedsActionGroupDto(GroupKey, 0, []);

        var rows = await Expiring(dates)
            .OrderBy(row => row.Document.ValidUntil).ThenBy(row => row.Document.Title).ThenBy(row => row.Document.Id).Take(take)
            .Select(row => new { row.Document.Id, row.Document.Title, row.Document.ValidUntil, ProjectId = row.Project.Id, ProjectName = row.Project.Name })
            .ToListAsync(cancellationToken);
        return new NeedsActionGroupDto(GroupKey, count,
            rows.Select(row => Row(row.Id, row.Title, row.ValidUntil!.Value, row.ProjectId, row.ProjectName, dates.Today)).ToArray());
    }

    /// <summary>"Expired 3 days ago, on 3 Dec 2026", "Expires today", "Expires in 12 days, on 15 Dec 2026"; urgent once expired. No amount.</summary>
    public static NeedsActionRowDto Row(Guid id, string title, DateOnly validUntil, Guid projectId, string projectName, DateOnly today)
    {
        var daysLeft = ProjectDocument.DaysLeft(validUntil, today);
        var detail = daysLeft switch
        {
            < 0 => $"Expired {Days(-daysLeft)} ago, on {Day(validUntil)}",
            0 => "Expires today",
            1 => "Expires tomorrow",
            _ => $"Expires in {Days(daysLeft)}, on {Day(validUntil)}"
        };
        return new NeedsActionRowDto(id, null, title, projectId, projectName, detail, null, validUntil,
            ProjectDocument.ExpiryFor(validUntil, today) == DocumentExpiry.Expired, null);
    }

    private IQueryable<ExpiringRow> Expiring(NeedsActionDates dates)
    {
        var until = dates.Today.AddDays(ProjectDocument.ExpiringSoonDays);
        var documents = db.Set<ProjectDocument>().AsNoTracking()
            .Where(document => document.Status == DocumentStatus.Active && document.ValidUntil != null && document.ValidUntil <= until);
        if (!Permissions.IsAllowed(currentUser.Roles, Permissions.CostsRead))
            documents = documents.Where(document => document.Category != DocumentCategory.Contract && document.Category != DocumentCategory.Insurance);
        return from document in documents
               join project in db.Set<Project>().AsNoTracking() on document.ProjectId equals project.Id
               where project.Status != ProjectStatus.Archived
               select new ExpiringRow { Document = document, Project = project };
    }

    private static string Days(int days) => days == 1 ? "1 day" : $"{days} days";

    private static string Day(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private sealed class ExpiringRow
    {
        public required ProjectDocument Document { get; init; }
        public required Project Project { get; init; }
    }
}
