using NexaOps.Domain.Enums;

namespace NexaOps.Application.Common.Interfaces;

public interface ICompanyAccessService
{
    Task<bool> HasReadAccessAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task EnsureReadAccessAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task EnsureCanManageDepartmentsAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task EnsureCanDeleteDepartmentsAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task EnsureCanManageMembersAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task EnsureCanManageFormsAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task EnsureCanReviewSubmissionsAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task EnsureCanViewAuditLogsAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task EnsureIsOwnerAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<bool> IsOwnerAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<MemberRole?> GetUserRoleInCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);
}
