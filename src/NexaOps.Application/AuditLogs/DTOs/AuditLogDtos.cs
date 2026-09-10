namespace NexaOps.Application.AuditLogs.DTOs;

public class AuditLogDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? ActorId { get; set; }
    public string? ActorName { get; set; }
    public string? ActorEmail { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public Dictionary<string, object?> Details { get; set; } = new();
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AuditLogQueryFilter
{
    public string? EntityType { get; set; }
    public string? Action { get; set; }
    public Guid? ActorId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
