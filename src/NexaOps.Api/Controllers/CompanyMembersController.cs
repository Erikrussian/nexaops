using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaOps.Application.CompanyMembers.DTOs;
using NexaOps.Application.CompanyMembers.Services;

namespace NexaOps.Api.Controllers;

[ApiController]
[Authorize]
public class CompanyMembersController : ControllerBase
{
    private readonly ICompanyMemberService _memberService;

    public CompanyMembersController(ICompanyMemberService memberService)
    {
        _memberService = memberService;
    }

    /// <summary>
    /// POST /companies/{companyId}/members/invite - Mời nhân viên tham gia công ty
    /// </summary>
    [HttpPost("companies/{companyId:guid}/members/invite")]
    [ProducesResponseType(typeof(InviteMemberResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InviteMemberResponse>> InviteMember(
        [FromRoute] Guid companyId,
        [FromBody] InviteMemberRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _memberService.InviteMemberAsync(companyId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// POST /company-members/accept-invite - Chấp nhận lời mời tham gia công ty
    /// </summary>
    [HttpPost("company-members/accept-invite")]
    [ProducesResponseType(typeof(AcceptInviteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AcceptInviteResponse>> AcceptInvite(
        [FromBody] AcceptInviteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _memberService.AcceptInviteAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /companies/{companyId}/members - Lấy danh sách thành viên trong công ty
    /// </summary>
    [HttpGet("companies/{companyId:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<CompanyMemberResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CompanyMemberResponse>>> GetMembersByCompany(
        [FromRoute] Guid companyId,
        CancellationToken cancellationToken)
    {
        var result = await _memberService.GetMembersByCompanyAsync(companyId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// PATCH /company-members/{memberId} - Cập nhật vai trò / phòng ban của thành viên
    /// </summary>
    [HttpPatch("company-members/{memberId:guid}")]
    [ProducesResponseType(typeof(CompanyMemberResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyMemberResponse>> UpdateMember(
        [FromRoute] Guid memberId,
        [FromBody] UpdateMemberRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _memberService.UpdateMemberAsync(memberId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// DELETE /company-members/{memberId} - Xóa thành viên khỏi công ty (Chặn xóa Owner)
    /// </summary>
    [HttpDelete("company-members/{memberId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RemoveMember(
        [FromRoute] Guid memberId,
        CancellationToken cancellationToken)
    {
        await _memberService.RemoveMemberAsync(memberId, cancellationToken);
        return Ok(new { message = "Đã xóa thành viên khỏi công ty thành công" });
    }
}
