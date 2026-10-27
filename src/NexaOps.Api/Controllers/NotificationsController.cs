using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaOps.Application.Common.Interfaces;
using NexaOps.Application.Notifications.DTOs;
using NexaOps.Application.Notifications.Services;

namespace NexaOps.Api.Controllers;

[ApiController]
[Authorize]
[Route("notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserService _currentUserService;

    public NotificationsController(
        INotificationService notificationService,
        ICurrentUserService currentUserService)
    {
        _notificationService = notificationService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// GET /notifications - Lấy danh sách thông báo của người dùng theo tổ chức
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(NotificationListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<NotificationListResponse>> GetNotifications(
        [FromQuery] Guid companyId,
        [FromQuery] bool? unreadOnly = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId!.Value;
        var result = await _notificationService.GetUserNotificationsAsync(
            currentUserId, companyId, unreadOnly, page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /notifications/unread-count - Lấy số lượng thông báo chưa đọc
    /// </summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<int>> GetUnreadCount(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId!.Value;
        var count = await _notificationService.GetUnreadCountAsync(currentUserId, companyId, cancellationToken);
        return Ok(count);
    }

    /// <summary>
    /// POST /notifications/{notificationId}/read - Đánh dấu thông báo đã đọc
    /// </summary>
    [HttpPost("{notificationId:guid}/read")]
    [ProducesResponseType(typeof(NotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationDto>> MarkAsRead(
        [FromRoute] Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId!.Value;
        var result = await _notificationService.MarkAsReadAsync(notificationId, currentUserId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// POST /notifications/read-all - Đánh dấu tất cả thông báo trong tổ chức đã đọc
    /// </summary>
    [HttpPost("read-all")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<int>> MarkAllAsRead(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId!.Value;
        var count = await _notificationService.MarkAllAsReadAsync(currentUserId, companyId, cancellationToken);
        return Ok(count);
    }

    /// <summary>
    /// DELETE /notifications/{notificationId} - Xóa một thông báo
    /// </summary>
    [HttpDelete("{notificationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteNotification(
        [FromRoute] Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId!.Value;
        await _notificationService.DeleteNotificationAsync(notificationId, currentUserId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// DELETE /notifications/clear-all - Xóa toàn bộ thông báo trong tổ chức của người dùng
    /// </summary>
    [HttpDelete("clear-all")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<int>> ClearAll(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId!.Value;
        var count = await _notificationService.ClearAllNotificationsAsync(currentUserId, companyId, cancellationToken);
        return Ok(count);
    }
}

