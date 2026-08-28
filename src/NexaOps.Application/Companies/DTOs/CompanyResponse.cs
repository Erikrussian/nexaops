using NexaOps.Domain.Entities;

namespace NexaOps.Application.Companies.DTOs;

public class CompanyResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string? Logo { get; init; }
    public string? Description { get; init; }
    public Guid OwnerId { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    public static CompanyResponse FromEntity(Company company) => new()
    {
        Id = company.Id,
        Name = company.Name,
        Slug = company.Slug,
        Code = company.Code,
        Logo = company.Logo,
        Description = company.Description,
        OwnerId = company.OwnerId,
        IsActive = company.IsActive,
        CreatedAt = company.CreatedAt,
        UpdatedAt = company.UpdatedAt
    };
}
