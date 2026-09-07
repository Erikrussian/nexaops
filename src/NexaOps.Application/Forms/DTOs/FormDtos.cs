using NexaOps.Domain.Enums;

namespace NexaOps.Application.Forms.DTOs;

public class FormFieldDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public FormFieldType Type { get; set; } = FormFieldType.Text;
    public bool Required { get; set; }
    public string? Placeholder { get; set; }
    public object? DefaultValue { get; set; }
    public List<string>? Options { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public string? Pattern { get; set; }
}

public class CreateFormDto
{
    public Guid? DepartmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<FormFieldDto> Fields { get; set; } = new();
}

public class UpdateFormDto
{
    public Guid? DepartmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<FormFieldDto> Fields { get; set; } = new();
}

public class ChangeFormStatusDto
{
    public FormStatus Status { get; set; }
}

public class FormDefinitionDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public FormStatus Status { get; set; }
    public int Version { get; set; }
    public List<FormFieldDto> Fields { get; set; } = new();
    public Guid CreatedById { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
