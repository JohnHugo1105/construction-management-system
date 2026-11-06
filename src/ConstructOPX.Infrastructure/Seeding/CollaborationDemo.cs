using ConstructOPX.Domain.Features.Collaboration;
using ConstructOPX.Domain.Features.Organizations;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Infrastructure.Persistence;

namespace ConstructOPX.Infrastructure.Seeding;

/// <summary>
/// Demo coordination for Riverside Residences (fictional): the project manager asks the cost engineer to check the rebar quantities, the cost
/// engineer asks the project manager to confirm a client request, and a short discussion on the project where the cost engineer mentions the
/// project manager. The notifications those steps would have caused are added too, unread. No amount appears anywhere. Saves what it adds.
/// </summary>
public static class CollaborationDemo
{
    /// <summary>
    /// Adds the demo tasks, the discussion and their notifications, and saves. Call it once from the development seeder after Riverside
    /// Residences and its people are saved, for example
    /// <c>await CollaborationDemo.SeedAsync(db, organization, riverside, (users["manager"].Id, users["manager"].DisplayName),
    /// (users["cost"].Id, users["cost"].DisplayName), today, now, cancellationToken);</c>
    /// </summary>
    /// <param name="riverside">The demo project; it must not be archived.</param>
    /// <param name="manager">The demo Project Manager (id and display name); holds tasks.use.</param>
    /// <param name="costEngineer">The demo Cost Engineer (id and display name); holds tasks.use.</param>
    /// <param name="today">The company's calendar day, so the due dates are in the future.</param>
    /// <param name="now">The seeding instant; the rows are dated a few hours before it.</param>
    public static async Task SeedAsync(ConstructOpxDbContext db, Organization organization, Project riverside, (Guid Id, string Name) manager,
        (Guid Id, string Name) costEngineer, DateOnly today, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var tenant = organization.Id;
        var rebar = WorkTask.Create(Guid.NewGuid(), tenant, "Check the rebar quantities against the structural drawings",
            "Compare the 16 mm and 12 mm bars on the purchase orders with revision C of the drawings before the next pour.", today.AddDays(3),
            new TaskPerson(costEngineer.Id, costEngineer.Name, true, true), manager.Id, riverside, new TaskLink(RecordKind.Project, riverside.Id), null,
            today, now.AddHours(-30));
        var waterproofing = WorkTask.Create(Guid.NewGuid(), tenant, "Confirm the client's basement waterproofing request",
            "The client asked about a membrane for the basement. Confirm the scope in writing so a change order can be drafted.", today.AddDays(1),
            new TaskPerson(manager.Id, manager.Name, true, true), costEngineer.Id, riverside, null, null, today, now.AddHours(-6));
        db.AddRange(rebar, waterproofing);

        var question = Comment.Create(Guid.NewGuid(), tenant, RecordKind.Project, riverside.Id, riverside,
            "The delivery of rebar for the second floor slab is set for Thursday. Can the pour move to the following Monday?",
            costEngineer.Id, [new MentionCandidate(manager.Id, manager.Name, true, true)], null, now.AddHours(-5));
        var answer = Comment.Create(Guid.NewGuid(), tenant, RecordKind.Project, riverside.Id, riverside,
            "Yes, Monday works. I will tell the site team and the ready-mix supplier.", manager.Id, [], null, now.AddHours(-4));
        db.AddRange(question, answer);

        db.AddRange(
            Notification.Create(Guid.NewGuid(), tenant, costEngineer.Id, NotificationKind.TaskAssigned,
                NotificationMessages.TaskAssigned(manager.Name, riverside.Name), RecordKind.Task, rebar.Id, riverside.Id, now.AddHours(-30)),
            Notification.Create(Guid.NewGuid(), tenant, manager.Id, NotificationKind.TaskAssigned,
                NotificationMessages.TaskAssigned(costEngineer.Name, riverside.Name), RecordKind.Task, waterproofing.Id, riverside.Id, now.AddHours(-6)),
            Notification.Create(Guid.NewGuid(), tenant, manager.Id, NotificationKind.Mention,
                NotificationMessages.Mention(costEngineer.Name, NotificationMessages.Project(riverside.Name)), RecordKind.Project, riverside.Id, riverside.Id,
                now.AddHours(-5)));
        await db.SaveChangesAsync(cancellationToken);
    }
}
