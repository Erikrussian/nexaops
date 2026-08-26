using NexaOps.Domain.Entities;
using NexaOps.Domain.Enums;

namespace NexaOps.Application.Users.DTOs;

public class UserResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string? Avatar { get; init; }
    public string? Phone { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    public static UserResponse FromEntity(User user) => new()
    {
        Id = user.Id,
        Name = user.Name,
        Email = user.Email,
        Role = user.Role.ToString().ToLower(),
        Avatar = user.Avatar,
        Phone = user.Phone,
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt
    };
}
