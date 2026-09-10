using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaOps.Application.AuditLogs.DTOs;
using NexaOps.Application.AuditLogs.Services;
using NexaOps.Application.Common.Models;

namespace NexaOps.Api.Controllers;

[ApiController]
[Authorize]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogsController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// GET /companies/{companyId}/audit-logs - Lấy danh sách lịch sử kiểm toán của công ty (Chỉ Owner & Admin)
    /// </summary>
    [HttpGet("companies/{companyId:guid}/audit-logs")]
    [ProducesResponseType(typeof(PagedResult<AuditLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> GetCompanyAuditLogs(
        [FromRoute] Guid companyId,
        [FromQuery] AuditLogQueryFilter filter,
        CancellationToken cancellationToken)
    {
        var result = await _auditLogService.GetCompanyAuditLogsAsync(companyId, filter, cancellationToken);
        return Ok(result);
    }
}
