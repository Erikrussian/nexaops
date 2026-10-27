using NexaOps.Application.Notifications.DTOs;

namespace NexaOps.Application.Notifications.Services;

public interface INotificationService
{
    Task<NotificationListResponse> GetUserNotificationsAsync(Guid userId, Guid companyId, bool? unreadOnly = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default);
    Task<NotificationDto> MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken = default);
    Task<int> MarkAllAsReadAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default);
    Task<NotificationDto> CreateNotificationAsync(CreateNotificationDto dto, CancellationToken cancellationToken = default);
    Task DeleteNotificationAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken = default);
    Task<int> ClearAllNotificationsAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default);
}
