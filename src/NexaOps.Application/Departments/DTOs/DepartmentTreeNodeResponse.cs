using NexaOps.Application.Users.DTOs;
using NexaOps.Domain.Entities;

namespace NexaOps.Application.Departments.DTOs;

public class DepartmentTreeNodeResponse
{
    public Guid Id { get; init; }
    public Guid CompanyId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public Guid? ParentId { get; init; }
    public Guid? ManagerId { get; init; }
    public UserResponse? Manager { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public List<DepartmentTreeNodeResponse> Children { get; set; } = new();

    public static DepartmentTreeNodeResponse FromEntity(Department dept) => new()
    {
        Id = dept.Id,
        CompanyId = dept.CompanyId,
        Name = dept.Name,
        Code = dept.Code,
        ParentId = dept.ParentId,
        ManagerId = dept.ManagerId,
        Manager = dept.Manager is not null ? UserResponse.FromEntity(dept.Manager) : null,
        Description = dept.Description,
        IsActive = dept.IsActive,
        CreatedAt = dept.CreatedAt,
        UpdatedAt = dept.UpdatedAt,
        Children = new List<DepartmentTreeNodeResponse>()
    };
}
