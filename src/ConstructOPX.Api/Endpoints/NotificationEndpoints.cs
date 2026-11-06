using ConstructOPX.Application.Common;
using ConstructOPX.Application.Features.Collaboration;
using Microsoft.AspNetCore.Mvc;

namespace ConstructOPX.Api.Endpoints;

/// <summary>
/// The signed-in person's own notifications (notifications.read, every role). Another person's notification is 404 even with its id. In-app
/// only: nothing here sends an email or a text message.
/// </summary>
public sealed class NotificationEndpoints : IEndpointModule
{
    public void Map(RouteGroupBuilder api)
    {
        var notifications = api.MapFeature("/notifications", "Notifications");

        notifications.MapGet("", async ([FromQuery] bool? unread, [AsParameters] PageQuery page, NotificationService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(unread, page.ToRequest(), ct))).RequirePermission(Permissions.NotificationsRead);

        // The bell: how many are unread.
        notifications.MapGet("/count", async (NotificationService service, CancellationToken ct) => Results.Ok(await service.CountAsync(ct)))
            .RequirePermission(Permissions.NotificationsRead);

        notifications.MapPost("/{id:guid}/read", async (Guid id, NotificationService service, CancellationToken ct) =>
            Results.Ok(await service.MarkReadAsync(id, ct))).RequirePermission(Permissions.NotificationsRead);

        notifications.MapPost("/read-all", async (NotificationService service, CancellationToken ct) =>
            Results.Ok(await service.MarkAllReadAsync(ct))).RequirePermission(Permissions.NotificationsRead);
    }
}
