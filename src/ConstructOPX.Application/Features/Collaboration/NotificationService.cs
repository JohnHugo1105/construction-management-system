using ConstructOPX.Application.Common;
using ConstructOPX.Domain.Common;

namespace ConstructOPX.Application.Features.Collaboration;

/// <summary>
/// The signed-in person's own notifications: read them, count the unread ones for the bell, and mark them read. Every read and write is
/// filtered to the caller, so another person's notification is "not found" even with its id. Notifications appear in the app only.
/// </summary>
public sealed class NotificationService(INotificationRepository notifications, INotificationReader reader, ICurrentUser currentUser, IClock clock,
    IUnitOfWork unitOfWork)
{
    public Task<PagedResult<NotificationDto>> ListAsync(bool? unreadOnly, PageRequest page, CancellationToken cancellationToken) =>
        reader.ListAsync(currentUser.UserId, unreadOnly == true, page, cancellationToken);

    public async Task<NotificationCountDto> CountAsync(CancellationToken cancellationToken) =>
        new(await reader.CountUnreadAsync(currentUser.UserId, cancellationToken));

    /// <summary>Marks one notification read (reading it again changes nothing) and answers with the new unread count for the bell.</summary>
    public async Task<NotificationCountDto> MarkReadAsync(Guid id, CancellationToken cancellationToken)
    {
        var notification = await notifications.GetOwnAsync(id, currentUser.UserId, cancellationToken) ?? throw new NotFoundException("that notification");
        if (notification.MarkRead(clock.UtcNow)) await unitOfWork.SaveChangesAsync(cancellationToken);
        return await CountAsync(cancellationToken);
    }

    public async Task<NotificationCountDto> MarkAllReadAsync(CancellationToken cancellationToken)
    {
        await notifications.MarkAllReadAsync(currentUser.UserId, clock.UtcNow, cancellationToken);
        return new NotificationCountDto(0);
    }
}
