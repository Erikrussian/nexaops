using System.ComponentModel.DataAnnotations;
using NexaOps.Domain.Enums;

namespace NexaOps.Application.CompanyMembers.DTOs;

public class InviteMemberRequest
{
    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    public string Email { get; set; } = string.Empty;

    public MemberRole? Role { get; set; } = MemberRole.Member;
    public Guid? DepartmentId { get; set; }
}
