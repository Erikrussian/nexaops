using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace NexaOps.Api.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? Context.UserIdentifier;

        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogInformation("SignalR client {ConnectionId} connected for user {UserId}", Context.ConnectionId, userId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? Context.UserIdentifier;

        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogInformation("SignalR client {ConnectionId} disconnected for user {UserId}", Context.ConnectionId, userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinCompany(string companyId)
    {
        if (Guid.TryParse(companyId, out var parsedCompanyId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"company_{parsedCompanyId}");
            _logger.LogInformation("Client {ConnectionId} joined company group {CompanyId}", Context.ConnectionId, parsedCompanyId);
        }
    }

    public async Task LeaveCompany(string companyId)
    {
        if (Guid.TryParse(companyId, out var parsedCompanyId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"company_{parsedCompanyId}");
            _logger.LogInformation("Client {ConnectionId} left company group {CompanyId}", Context.ConnectionId, parsedCompanyId);
        }
    }
}
