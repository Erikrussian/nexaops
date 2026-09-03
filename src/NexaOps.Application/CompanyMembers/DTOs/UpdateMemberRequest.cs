using NexaOps.Domain.Enums;

namespace NexaOps.Application.CompanyMembers.DTOs;

public class UpdateMemberRequest
{
    public MemberRole? Role { get; set; }
    public MemberStatus? Status { get; set; }
    public Guid? DepartmentId { get; set; }
}
