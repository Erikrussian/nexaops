namespace NexaOps.Application.Auth.DTOs;

public class UserSessionResponse
{
    public Guid Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public string? CreatedByIp { get; init; }
    public string? UserAgent { get; init; }
    public bool IsCurrent { get; init; }
}
