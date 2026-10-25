using Microsoft.AspNetCore.SignalR;
using NexaOps.Api.Hubs;
using NexaOps.Application.Notifications.Services;

namespace NexaOps.Api.Services;

public class RealtimeNotificationService : IRealtimeNotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<RealtimeNotificationService> _logger;

    public RealtimeNotificationService(
        IHubContext<NotificationHub> hubContext,
        ILogger<RealtimeNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendNotificationToUserAsync(Guid userId, object notificationPayload, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients
                .Group($"user_{userId}")
                .SendAsync("ReceiveNotification", notificationPayload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send real-time notification to user {UserId}", userId);
        }
    }

    public async Task SendNotificationToCompanyAsync(Guid companyId, object notificationPayload, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients
                .Group($"company_{companyId}")
                .SendAsync("ReceiveNotification", notificationPayload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send real-time notification to company {CompanyId}", companyId);
        }
    }

    public async Task BroadcastWorkflowEventAsync(Guid companyId, string eventName, object payload, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients
                .Group($"company_{companyId}")
                .SendAsync(eventName, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast real-time workflow event {EventName} to company {CompanyId}", eventName, companyId);
        }
    }
}
