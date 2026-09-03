using NexaOps.Application.CompanyMembers.DTOs;

namespace NexaOps.Application.CompanyMembers.Services;

public interface ICompanyMemberService
{
    Task<InviteMemberResponse> InviteMemberAsync(Guid companyId, InviteMemberRequest request, CancellationToken cancellationToken = default);
    Task<AcceptInviteResponse> AcceptInviteAsync(AcceptInviteRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompanyMemberResponse>> GetMembersByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<CompanyMemberResponse> UpdateMemberAsync(Guid memberId, UpdateMemberRequest request, CancellationToken cancellationToken = default);
    Task RemoveMemberAsync(Guid memberId, CancellationToken cancellationToken = default);
}
