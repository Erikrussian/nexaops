using NexaOps.Application.Departments.DTOs;
using NexaOps.Application.Users.DTOs;
using NexaOps.Domain.Entities;

namespace NexaOps.Application.CompanyMembers.DTOs;

public class CompanyMemberResponse
{
    public Guid Id { get; init; }
    public Guid CompanyId { get; init; }
    public Guid? UserId { get; init; }
    public UserResponse? User { get; init; }
    public string Email { get; init; } = string.Empty;
    public Guid? DepartmentId { get; init; }
    public DepartmentResponse? Department { get; init; }
    public string Role { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? InviteExpiresAt { get; init; }
    public Guid? InvitedById { get; init; }
    public UserResponse? InvitedBy { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    public static CompanyMemberResponse FromEntity(CompanyMember member) => new()
    {
        Id = member.Id,
        CompanyId = member.CompanyId,
        UserId = member.UserId,
        User = member.User is not null ? UserResponse.FromEntity(member.User) : null,
        Email = member.Email,
        DepartmentId = member.DepartmentId,
        Department = member.Department is not null ? DepartmentResponse.FromEntity(member.Department) : null,
        Role = member.Role.ToString().ToUpperInvariant(),
        Status = member.Status.ToString().ToUpperInvariant(),
        InviteExpiresAt = member.InviteExpiresAt,
        InvitedById = member.InvitedById,
        InvitedBy = member.InvitedBy is not null ? UserResponse.FromEntity(member.InvitedBy) : null,
        CreatedAt = member.CreatedAt,
        UpdatedAt = member.UpdatedAt
    };
}
