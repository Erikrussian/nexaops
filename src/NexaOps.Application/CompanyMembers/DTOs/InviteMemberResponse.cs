namespace NexaOps.Application.CompanyMembers.DTOs;

public class InviteMemberResponse
{
    public string Message { get; init; } = "Đã gửi lời mời tham gia công ty";
    public CompanyMemberResponse Member { get; init; } = default!;
    public string InviteLink { get; init; } = string.Empty;
}
