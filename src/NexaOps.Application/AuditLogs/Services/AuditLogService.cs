using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NexaOps.Application.AuditLogs.DTOs;
using NexaOps.Application.Common.Interfaces;
using NexaOps.Application.Common.Models;
using NexaOps.Domain.Entities;

namespace NexaOps.Application.AuditLogs.Services;

public interface IAuditLogService
{
    Task LogAsync(
        Guid companyId,
        Guid? actorId,
        string action,
        string entityType,
        string entityId,
        object? details = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AuditLogDto>> GetCompanyAuditLogsAsync(
        Guid companyId,
        AuditLogQueryFilter filter,
        CancellationToken cancellationToken = default);
}

public class AuditLogService : IAuditLogService
{
    private readonly IApplicationDbContext _context;
    private readonly ICompanyAccessService _companyAccessService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AuditLogService(
        IApplicationDbContext context,
        ICompanyAccessService companyAccessService)
    {
        _context = context;
        _companyAccessService = companyAccessService;
    }

    public async Task LogAsync(
        Guid companyId,
        Guid? actorId,
        string action,
        string entityType,
        string entityId,
        object? details = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        var detailsJson = details != null
            ? JsonSerializer.Serialize(details, JsonOptions)
            : "{}";

        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ActorId = actorId,
            Action = action.Trim().ToUpperInvariant(),
            EntityType = entityType.Trim(),
            EntityId = entityId.Trim(),
            DetailsJson = detailsJson,
            IpAddress = ipAddress?.Trim(),
            UserAgent = userAgent?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<AuditLogDto>> GetCompanyAuditLogsAsync(
        Guid companyId,
        AuditLogQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        await _companyAccessService.EnsureCanViewAuditLogsAsync(companyId, cancellationToken);

        var query = _context.AuditLogs
            .Include(a => a.Actor)
            .Where(a => a.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            var entityType = filter.EntityType.Trim();
            query = query.Where(a => a.EntityType.ToLower() == entityType.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            var action = filter.Action.Trim().ToUpperInvariant();
            query = query.Where(a => a.Action == action);
        }

        if (filter.ActorId.HasValue)
        {
            query = query.Where(a => a.ActorId == filter.ActorId.Value);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt <= filter.ToDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 100 ? 20 : filter.PageSize;

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(MapToDto).ToList();

        return new PagedResult<AuditLogDto>(dtos, totalCount, page, pageSize);
    }

    private static AuditLogDto MapToDto(AuditLog log)
    {
        Dictionary<string, object?> details;
        try
        {
            details = JsonSerializer.Deserialize<Dictionary<string, object?>>(log.DetailsJson, JsonOptions)
                      ?? new Dictionary<string, object?>();
        }
        catch
        {
            details = new Dictionary<string, object?>();
        }

        return new AuditLogDto
        {
            Id = log.Id,
            CompanyId = log.CompanyId,
            ActorId = log.ActorId,
            ActorName = log.Actor?.Name,
            ActorEmail = log.Actor?.Email,
            Action = log.Action,
            EntityType = log.EntityType,
            EntityId = log.EntityId,
            Details = details,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
            CreatedAt = log.CreatedAt
        };
    }
}
