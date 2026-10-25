namespace NexaOps.Application.Notifications.Services;

public interface IRealtimeNotificationService
{
    Task SendNotificationToUserAsync(Guid userId, object notificationPayload, CancellationToken cancellationToken = default);
    Task SendNotificationToCompanyAsync(Guid companyId, object notificationPayload, CancellationToken cancellationToken = default);
    Task BroadcastWorkflowEventAsync(Guid companyId, string eventName, object payload, CancellationToken cancellationToken = default);
}
