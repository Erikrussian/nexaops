using NexaOps.Application.Departments.DTOs;

namespace NexaOps.Application.Departments.Services;

public interface IDepartmentService
{
    Task<DepartmentResponse> CreateAsync(Guid companyId, CreateDepartmentRequest request, CancellationToken cancellationToken = default);
    Task<object> FindAllByCompanyAsync(Guid companyId, bool tree = false, CancellationToken cancellationToken = default);
    Task<DepartmentResponse> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DepartmentResponse> UpdateAsync(Guid id, UpdateDepartmentRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid id, CancellationToken cancellationToken = default);
}
