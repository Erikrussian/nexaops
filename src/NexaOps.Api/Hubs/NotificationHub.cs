using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NexaOps.Application.Common.Interfaces;
using NexaOps.Domain.Enums;

namespace NexaOps.Api.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(IApplicationDbContext context, ILogger<NotificationHub> logger)
    {
        _context = context;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId()?.ToString()
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
        var userId = GetCurrentUserId()?.ToString()
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
        if (string.IsNullOrWhiteSpace(companyId) || !Guid.TryParse(companyId, out var parsedCompanyId) || parsedCompanyId == Guid.Empty)
        {
            _logger.LogWarning("Connection {ConnectionId} attempted to join company with invalid companyId: {CompanyId}", Context.ConnectionId, companyId);
            throw new HubException("Invalid company identifier.");
        }

        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            _logger.LogWarning("Connection {ConnectionId} attempted to join company {CompanyId} without authenticated user", Context.ConnectionId, parsedCompanyId);
            throw new HubException("Unauthorized: Authentication is required.");
        }

        // Verify current active company membership or ownership using trusted server-side data
        var isOwner = await _context.Companies
            .AsNoTracking()
            .AnyAsync(c => c.Id == parsedCompanyId && c.OwnerId == userId.Value);

        var isActiveMember = false;
        if (!isOwner)
        {
            isActiveMember = await _context.CompanyMembers
                .AsNoTracking()
                .AnyAsync(m => m.CompanyId == parsedCompanyId
                            && m.UserId == userId.Value
                            && m.Status == MemberStatus.Active);
        }

        if (!isOwner && !isActiveMember)
        {
            _logger.LogWarning("User {UserId} unauthorized to join company {CompanyId} via connection {ConnectionId}", userId.Value, parsedCompanyId, Context.ConnectionId);
            throw new HubException("Forbidden: You do not have access to this company.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"company_{parsedCompanyId}");
        _logger.LogInformation("Client {ConnectionId} (User {UserId}) authorized and joined company group {CompanyId}", Context.ConnectionId, userId.Value, parsedCompanyId);
    }

    public async Task LeaveCompany(string companyId)
    {
        if (Guid.TryParse(companyId, out var parsedCompanyId) && parsedCompanyId != Guid.Empty)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"company_{parsedCompanyId}");
            _logger.LogInformation("Client {ConnectionId} left company group {CompanyId}", Context.ConnectionId, parsedCompanyId);
        }
    }

    private Guid? GetCurrentUserId()
    {
        var sub = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? Context.User?.FindFirst("sub")?.Value
                  ?? Context.UserIdentifier;

        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
