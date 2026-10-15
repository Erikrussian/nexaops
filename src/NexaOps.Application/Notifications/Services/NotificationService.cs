using Microsoft.EntityFrameworkCore;
using NexaOps.Application.Common.Exceptions;
using NexaOps.Application.Common.Interfaces;
using NexaOps.Application.Notifications.DTOs;
using NexaOps.Domain.Entities;

namespace NexaOps.Application.Notifications.Services;

public class NotificationService : INotificationService
{
    private readonly IApplicationDbContext _context;

    public NotificationService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<NotificationListResponse> GetUserNotificationsAsync(
        Guid userId,
        Guid companyId,
        bool? unreadOnly = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Notifications
            .Where(n => n.UserId == userId && n.CompanyId == companyId);

        if (unreadOnly == true)
        {
            query = query.Where(n => !n.IsRead);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var unreadCount = await _context.Notifications
            .CountAsync(n => n.UserId == userId && n.CompanyId == companyId && !n.IsRead, cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                UserId = n.UserId,
                CompanyId = n.CompanyId,
                Type = n.Type,
                Title = n.Title,
                Message = n.Message,
                LinkUrl = n.LinkUrl,
                IsRead = n.IsRead,
                ReadAt = n.ReadAt,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new NotificationListResponse
        {
            Items = items,
            UnreadCount = unreadCount,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default)
    {
        return await _context.Notifications
            .CountAsync(n => n.UserId == userId && n.CompanyId == companyId && !n.IsRead, cancellationToken);
    }

    public async Task<NotificationDto> MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);

        if (notification == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy thông báo với ID: {notificationId}");
        }

        if (notification.UserId != userId)
        {
            throw new ForbiddenAccessException("Bạn không có quyền cập nhật thông báo của người dùng khác");
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new NotificationDto
        {
            Id = notification.Id,
            UserId = notification.UserId,
            CompanyId = notification.CompanyId,
            Type = notification.Type,
            Title = notification.Title,
            Message = notification.Message,
            LinkUrl = notification.LinkUrl,
            IsRead = notification.IsRead,
            ReadAt = notification.ReadAt,
            CreatedAt = notification.CreatedAt
        };
    }

    public async Task<int> MarkAllAsReadAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default)
    {
        var unreadNotifications = await _context.Notifications
            .Where(n => n.UserId == userId && n.CompanyId == companyId && !n.IsRead)
            .ToListAsync(cancellationToken);

        if (unreadNotifications.Count == 0)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return unreadNotifications.Count;
    }

    public async Task<NotificationDto> CreateNotificationAsync(CreateNotificationDto dto, CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = dto.UserId,
            CompanyId = dto.CompanyId,
            Type = dto.Type,
            Title = dto.Title.Trim(),
            Message = dto.Message.Trim(),
            LinkUrl = dto.LinkUrl?.Trim(),
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);

        return new NotificationDto
        {
            Id = notification.Id,
            UserId = notification.UserId,
            CompanyId = notification.CompanyId,
            Type = notification.Type,
            Title = notification.Title,
            Message = notification.Message,
            LinkUrl = notification.LinkUrl,
            IsRead = notification.IsRead,
            ReadAt = notification.ReadAt,
            CreatedAt = notification.CreatedAt
        };
    }
}
