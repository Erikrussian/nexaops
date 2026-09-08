using NexaOps.Domain.Enums;

namespace NexaOps.Application.Companies.DTOs;

public class TransferOwnershipRequest
{
    public Guid NewOwnerId { get; set; }
    public string Password { get; set; } = string.Empty;
    public MemberRole PreviousOwnerNewRole { get; set; } = MemberRole.Admin;
}
