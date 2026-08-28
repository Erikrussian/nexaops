using NexaOps.Application.Companies.DTOs;

namespace NexaOps.Application.Companies.Services;

public interface ICompanyService
{
    Task<CompanyResponse> CreateAsync(CreateCompanyRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompanyResponse>> FindMyCompaniesAsync(CancellationToken cancellationToken = default);
    Task<CompanyResponse> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CompanyResponse> FindBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<CompanyResponse> UpdateAsync(Guid id, UpdateCompanyRequest request, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid id, CancellationToken cancellationToken = default);
}
