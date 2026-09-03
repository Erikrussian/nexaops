using NexaOps.Domain.Enums;

namespace NexaOps.Domain.Entities;

public class CompanyMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public string Email { get; set; } = string.Empty;
    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public MemberRole Role { get; set; } = MemberRole.Member;
    public MemberStatus Status { get; set; } = MemberStatus.Invited;
    public string? InviteToken { get; set; }
    public DateTime? InviteExpiresAt { get; set; }
    public Guid? InvitedById { get; set; }
    public User? InvitedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
