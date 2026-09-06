using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using NexaOps.Application.Common.Exceptions;
using NexaOps.Application.Common.Interfaces;
using NexaOps.Application.Companies.DTOs;
using NexaOps.Application.CompanyMembers.DTOs;
using NexaOps.Domain.Entities;
using NexaOps.Domain.Enums;

namespace NexaOps.Application.CompanyMembers.Services;

public class CompanyMemberService : ICompanyMemberService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICompanyAccessService _companyAccessService;

    public CompanyMemberService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICompanyAccessService companyAccessService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _companyAccessService = companyAccessService;
    }

    public async Task<InviteMemberResponse> InviteMemberAsync(Guid companyId, InviteMemberRequest request, CancellationToken cancellationToken = default)
    {
        await _companyAccessService.EnsureCanManageMembersAsync(companyId, cancellationToken);

        if (request.Role == MemberRole.Owner)
        {
            throw new InvalidOperationException("Không thể gán vai trò OWNER thông qua lời mời. Chuyển quyền sở hữu phải thực hiện qua quy trình riêng.");
        }

        var company = await _context.Companies
            .FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);

        if (company is null)
        {
            throw new KeyNotFoundException("Công ty không tồn tại");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var existingMember = await _context.CompanyMembers
            .FirstOrDefaultAsync(m => m.CompanyId == companyId && m.Email == normalizedEmail, cancellationToken);

        if (existingMember is not null && existingMember.Status == MemberStatus.Active)
        {
            throw new InvalidOperationException("Người dùng này đã là thành viên chính thức của công ty");
        }

        if (request.DepartmentId.HasValue)
        {
            var deptExists = await _context.Departments
                .AnyAsync(d => d.Id == request.DepartmentId.Value && d.CompanyId == companyId, cancellationToken);

            if (!deptExists)
            {
                throw new KeyNotFoundException("Phòng ban không tồn tại trong công ty này");
            }
        }

        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var inviteToken = Convert.ToHexString(tokenBytes).ToLowerInvariant();
        var inviteExpiresAt = DateTime.UtcNow.AddDays(7);

        CompanyMember member;

        if (existingMember is not null)
        {
            existingMember.Role = request.Role ?? MemberRole.Member;
            existingMember.DepartmentId = request.DepartmentId;
            existingMember.InviteToken = inviteToken;
            existingMember.InviteExpiresAt = inviteExpiresAt;
            existingMember.InvitedById = _currentUserService.UserId!.Value;
            existingMember.Status = MemberStatus.Invited;
            existingMember.UpdatedAt = DateTime.UtcNow;
            member = existingMember;
        }
        else
        {
            member = new CompanyMember
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                UserId = existingUser?.Id,
                Email = normalizedEmail,
                DepartmentId = request.DepartmentId,
                Role = request.Role ?? MemberRole.Member,
                Status = MemberStatus.Invited,
                InviteToken = inviteToken,
                InviteExpiresAt = inviteExpiresAt,
                InvitedById = _currentUserService.UserId!.Value,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.CompanyMembers.Add(member);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new InviteMemberResponse
        {
            Message = "Đã gửi lời mời tham gia công ty",
            Member = CompanyMemberResponse.FromEntity(member),
            InviteLink = $"http://localhost:3000/auth/invite?token={inviteToken}"
        };
    }

    public async Task<AcceptInviteResponse> AcceptInviteAsync(AcceptInviteRequest request, CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("Vui lòng đăng nhập để thực hiện thao tác này");
        }

        var member = await _context.CompanyMembers
            .Include(m => m.Company)
            .FirstOrDefaultAsync(m => m.InviteToken == request.InviteToken.Trim(), cancellationToken);

        if (member is null)
        {
            throw new KeyNotFoundException("Lời mời không hợp lệ hoặc đã được sử dụng");
        }

        if (member.InviteExpiresAt.HasValue && member.InviteExpiresAt.Value < DateTime.UtcNow)
        {
            throw new InvalidOperationException("Lời mời này đã hết hạn");
        }

        var currentUserEmail = _currentUserService.Email ?? string.Empty;
        if (!string.Equals(member.Email, currentUserEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"Lời mời này dành cho email {member.Email}, không phải tài khoản hiện tại ({currentUserEmail})");
        }

        member.UserId = _currentUserService.UserId.Value;
        member.Status = MemberStatus.Active;
        member.InviteToken = null;
        member.InviteExpiresAt = null;
        member.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return new AcceptInviteResponse
        {
            Message = "Bạn đã gia nhập công ty thành công!",
            Company = CompanyResponse.FromEntity(member.Company!),
            Role = member.Role.ToString().ToUpperInvariant()
        };
    }

    public async Task<IReadOnlyList<CompanyMemberResponse>> GetMembersByCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        await _companyAccessService.EnsureCanManageMembersAsync(companyId, cancellationToken);

        var members = await _context.CompanyMembers
            .Include(m => m.User)
            .Include(m => m.Department)
            .Include(m => m.InvitedBy)
            .AsNoTracking()
            .Where(m => m.CompanyId == companyId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => CompanyMemberResponse.FromEntity(m))
            .ToListAsync(cancellationToken);

        return members;
    }

    public async Task<CompanyMemberResponse> UpdateMemberAsync(Guid memberId, UpdateMemberRequest request, CancellationToken cancellationToken = default)
    {
        var member = await _context.CompanyMembers
            .Include(m => m.User)
            .Include(m => m.Department)
            .Include(m => m.InvitedBy)
            .FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken);

        if (member is null)
        {
            throw new KeyNotFoundException("Không tìm thấy thành viên này");
        }

        await _companyAccessService.EnsureCanManageMembersAsync(member.CompanyId, cancellationToken);

        var isOwner = await _companyAccessService.IsOwnerAsync(member.CompanyId, cancellationToken);

        // Admin cannot edit Owner
        if (member.Role == MemberRole.Owner && !isOwner)
        {
            throw new ForbiddenAccessException("Admin không được phép chỉnh sửa Owner của công ty");
        }

        if (request.Role == MemberRole.Owner)
        {
            throw new InvalidOperationException("Không thể gán vai trò OWNER thông qua cập nhật thành viên.");
        }

        // A user cannot elevate their own role
        if (member.UserId.HasValue && member.UserId.Value == _currentUserService.UserId && request.Role.HasValue && request.Role.Value != member.Role)
        {
            throw new ForbiddenAccessException("Người dùng không được tự thay đổi vai trò của chính mình");
        }

        if (request.DepartmentId.HasValue)
        {
            var deptExists = await _context.Departments
                .AnyAsync(d => d.Id == request.DepartmentId.Value && d.CompanyId == member.CompanyId, cancellationToken);

            if (!deptExists)
            {
                throw new KeyNotFoundException("Phòng ban không tồn tại trong công ty này");
            }

            member.DepartmentId = request.DepartmentId.Value;
        }

        if (request.Role.HasValue)
        {
            member.Role = request.Role.Value;
        }

        if (request.Status.HasValue)
        {
            member.Status = request.Status.Value;
        }

        member.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return CompanyMemberResponse.FromEntity(member);
    }

    public async Task RemoveMemberAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        var member = await _context.CompanyMembers
            .FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken);

        if (member is null)
        {
            throw new KeyNotFoundException("Không tìm thấy thành viên này");
        }

        await _companyAccessService.EnsureCanManageMembersAsync(member.CompanyId, cancellationToken);

        if (member.Role == MemberRole.Owner)
        {
            throw new InvalidOperationException("Không thể xóa người sở hữu (OWNER) của công ty");
        }

        var isOwner = await _companyAccessService.IsOwnerAsync(member.CompanyId, cancellationToken);
        if (!isOwner && member.Role == MemberRole.Owner)
        {
            throw new ForbiddenAccessException("Admin không được phép xóa Owner của công ty");
        }

        _context.CompanyMembers.Remove(member);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
