using NexaOps.Application.Companies.DTOs;

namespace NexaOps.Application.CompanyMembers.DTOs;

public class AcceptInviteResponse
{
    public string Message { get; init; } = "Bạn đã gia nhập công ty thành công!";
    public CompanyResponse Company { get; init; } = default!;
    public string Role { get; init; } = string.Empty;
}
