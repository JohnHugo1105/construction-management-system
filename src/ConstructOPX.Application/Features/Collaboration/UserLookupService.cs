using ConstructOPX.Application.Common;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Collaboration;

namespace ConstructOPX.Application.Features.Collaboration;

/// <summary>
/// Finds colleagues by name for the task assignee picker and the "@" mention picker. It returns only the id and the name of active colleagues:
/// never a role, an email or a username. With no record given, only the people who can work on tasks; with a record, only the people who
/// could read it (so nobody is offered who would then be refused).
/// </summary>
public sealed class UserLookupService(IColleagueReader colleagues, ICollaborationTargetReader targets, ICurrentUser currentUser)
{
    public const int MaximumResults = 20;

    public async Task<IReadOnlyList<ColleagueDto>> LookupAsync(string? search, string? targetKind, Guid? targetId, CancellationToken cancellationToken)
    {
        var text = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (text is { Length: > 120 }) throw new ValidationFailedException("search", "Search with at most 120 characters.");
        if (string.IsNullOrWhiteSpace(targetKind) && targetId is null)
            return await colleagues.SearchAsync(text, Permissions.RolesFor(Permissions.TasksUse), [], MaximumResults, cancellationToken);

        var kind = CollaborationAccess.ParseKind(targetKind, "targetKind", RecordKinds.CanCarryComments,
            "Choose the record: Project, Task, PurchaseOrder, ChangeOrder, ClientBill or Subcontract.");
        if (targetId is not { } id || id == Guid.Empty) throw new ValidationFailedException("targetId", "Choose the record.");
        if (!CollaborationAccess.CanReadKind(currentUser.Roles, kind)) throw new UnauthorizedAccessException("You can't open this record.");
        var target = await targets.FindAsync(kind, id, cancellationToken);
        if (target is null || !CollaborationAccess.CanRead(currentUser.UserId, currentUser.Roles, target)) throw new NotFoundException("that record");

        var alsoUsers = kind == RecordKind.Task ? new[] { target.TaskCreatorId, target.TaskAssigneeId }.OfType<Guid>().ToArray() : [];
        return await colleagues.SearchAsync(text, CollaborationAccess.ReadingRoles(kind), alsoUsers, MaximumResults, cancellationToken);
    }
}
