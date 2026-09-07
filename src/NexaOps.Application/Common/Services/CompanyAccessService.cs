using Microsoft.EntityFrameworkCore;
using NexaOps.Application.Common.Exceptions;
using NexaOps.Application.Common.Interfaces;
using NexaOps.Domain.Enums;

namespace NexaOps.Application.Common.Services;

public class CompanyAccessService : ICompanyAccessService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CompanyAccessService(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    private Guid GetCurrentUserId()
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("Vui lòng đăng nhập để thực hiện thao tác này");
        }
        return _currentUserService.UserId.Value;
    }

    private async Task<(bool IsOwner, MemberRole? MemberRole)> ResolveAccessAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        var company = await _context.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);

        if (company is null)
        {
            throw new KeyNotFoundException($"Không tìm thấy công ty với ID: {companyId}");
        }

        if (company.OwnerId == userId)
        {
            return (true, MemberRole.Owner);
        }

        var member = await _context.CompanyMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.CompanyId == companyId
                                   && m.UserId == userId
                                   && m.Status == MemberStatus.Active, cancellationToken);

        if (member is not null)
        {
            return (false, member.Role);
        }

        return (false, null);
    }

    public async Task<bool> HasReadAccessAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            return false;
        }

        var (isOwner, memberRole) = await ResolveAccessAsync(companyId, cancellationToken);
        return isOwner || memberRole.HasValue;
    }

    public async Task EnsureReadAccessAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var (isOwner, memberRole) = await ResolveAccessAsync(companyId, cancellationToken);
        if (!isOwner && !memberRole.HasValue)
        {
            throw new ForbiddenAccessException("Bạn không có quyền truy cập dữ liệu của công ty này");
        }
    }

    public async Task EnsureCanManageDepartmentsAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var (isOwner, memberRole) = await ResolveAccessAsync(companyId, cancellationToken);
        if (isOwner) return;

        if (memberRole is MemberRole.Admin or MemberRole.Manager)
        {
            return;
        }

        throw new ForbiddenAccessException("Chỉ Owner, Admin hoặc Manager mới có quyền tạo hoặc cập nhật phòng ban");
    }

    public async Task EnsureCanDeleteDepartmentsAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var (isOwner, memberRole) = await ResolveAccessAsync(companyId, cancellationToken);
        if (isOwner) return;

        if (memberRole is MemberRole.Admin)
        {
            return;
        }

        throw new ForbiddenAccessException("Chỉ Owner hoặc Admin mới có quyền xóa phòng ban");
    }

    public async Task EnsureCanManageMembersAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var (isOwner, memberRole) = await ResolveAccessAsync(companyId, cancellationToken);
        if (isOwner) return;

        if (memberRole is MemberRole.Admin)
        {
            return;
        }

        throw new ForbiddenAccessException("Chỉ Owner hoặc Admin mới có quyền quản lý thành viên");
    }

    public async Task EnsureCanManageFormsAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var (isOwner, memberRole) = await ResolveAccessAsync(companyId, cancellationToken);
        if (isOwner) return;

        if (memberRole is MemberRole.Admin or MemberRole.Manager)
        {
            return;
        }

        throw new ForbiddenAccessException("Chỉ Owner, Admin hoặc Manager mới có quyền tạo hoặc chỉnh sửa biểu mẫu");
    }

    public async Task EnsureCanReviewSubmissionsAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var (isOwner, memberRole) = await ResolveAccessAsync(companyId, cancellationToken);
        if (isOwner) return;

        if (memberRole is MemberRole.Admin or MemberRole.Manager)
        {
            return;
        }

        throw new ForbiddenAccessException("Chỉ Owner, Admin hoặc Manager mới có quyền duyệt đơn");
    }

    public async Task EnsureIsOwnerAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var (isOwner, _) = await ResolveAccessAsync(companyId, cancellationToken);
        if (!isOwner)
        {
            throw new ForbiddenAccessException("Chỉ Owner mới có quyền thực hiện thao tác này");
        }
    }

    public async Task<bool> IsOwnerAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var (isOwner, _) = await ResolveAccessAsync(companyId, cancellationToken);
        return isOwner;
    }

    public async Task<MemberRole?> GetUserRoleInCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var (_, memberRole) = await ResolveAccessAsync(companyId, cancellationToken);
        return memberRole;
    }
}
