using NexaOps.Domain.Enums;

namespace NexaOps.Domain.Entities;

public class FormDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public FormStatus Status { get; set; } = FormStatus.Draft;
    public int Version { get; set; } = 1;
    public string SchemaJson { get; set; } = "[]";
    public Guid CreatedById { get; set; }
    public User? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
