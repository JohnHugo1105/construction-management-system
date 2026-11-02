using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Collaboration;
using ConstructOPX.Application.Features.NeedsAction;
using ConstructOPX.Domain.Features.Collaboration;
using ConstructOPX.Domain.Features.Identity;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstructOPX.Infrastructure.Features.Collaboration;

/// <summary>
/// "Tasks assigned to you" on the Needs your action page: the caller's own Open tasks, overdue first (urgent), then the soonest due, then those
/// with no due date. A task on an archived project is read-only and is left out. A row carries the title, the project and the due date in
/// words, never an amount. One statement for the count, and one for the rows only when there is something.
/// </summary>
internal sealed class TasksAssignedNeedsActionSource(ConstructOpxDbContext db, ICurrentUser currentUser) : INeedsActionSource
{
    public string Key => "tasks-assigned";

    public bool CanActOn(AccessRole roles) => Permissions.IsAllowed(roles, Permissions.TasksUse);

    public Task<int> CountAsync(NeedsActionDates dates, CancellationToken cancellationToken) => Waiting().CountAsync(cancellationToken);

    public async Task<NeedsActionGroupDto> ReadAsync(NeedsActionDates dates, int take, CancellationToken cancellationToken)
    {
        var count = await CountAsync(dates, cancellationToken);
        if (count == 0) return new NeedsActionGroupDto(Key, 0, []);

        var today = dates.Today;
        var projects = db.Set<Project>();
        var rows = await Waiting()
            .OrderBy(task => task.DueDate != null && task.DueDate < today ? 0 : task.DueDate != null ? 1 : 2)
            .ThenBy(task => task.DueDate)
            .ThenBy(task => task.CreatedAtUtc)
            .ThenBy(task => task.Id)
            .Take(take)
            .Select(task => new
            {
                task.Id,
                task.Title,
                task.ProjectId,
                ProjectName = projects.Where(project => project.Id == task.ProjectId).Select(project => project.Name).FirstOrDefault(),
                task.CreatedAtUtc,
                task.DueDate
            }).ToListAsync(cancellationToken);

        return new NeedsActionGroupDto(Key, count, rows.Select(row => new NeedsActionRowDto(row.Id, null, row.Title, row.ProjectId, row.ProjectName,
            TaskWords.DueDetail(row.DueDate, today), row.CreatedAtUtc, row.DueDate, WorkTask.IsOverdueOn(WorkTaskStatus.Open, row.DueDate, today), null))
            .ToArray());
    }

    /// <summary>Open tasks assigned to the caller, not on an archived project.</summary>
    private IQueryable<WorkTask> Waiting()
    {
        var userId = currentUser.UserId;
        var projects = db.Set<Project>();
        return db.Set<WorkTask>().AsNoTracking()
            .Where(task => task.AssigneeUserId == userId && task.Status == WorkTaskStatus.Open &&
                (task.ProjectId == null || !projects.Any(project => project.Id == task.ProjectId && project.Status == ProjectStatus.Archived)));
    }
}
