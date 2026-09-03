using System.ComponentModel.DataAnnotations;

namespace NexaOps.Application.CompanyMembers.DTOs;

public class AcceptInviteRequest
{
    [Required(ErrorMessage = "Token không được để trống")]
    public string InviteToken { get; set; } = string.Empty;
}
