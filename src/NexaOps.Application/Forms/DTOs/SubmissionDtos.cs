using NexaOps.Domain.Enums;

namespace NexaOps.Application.Forms.DTOs;

public class SubmitFormDto
{
    public Dictionary<string, object?> Data { get; set; } = new();
}

public class ReviewSubmissionDto
{
    public SubmissionStatus Status { get; set; }
    public string? Notes { get; set; }
}

public class FormSubmissionDto
{
    public Guid Id { get; set; }
    public Guid FormDefinitionId { get; set; }
    public string FormTitle { get; set; } = string.Empty;
    public string FormCode { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public Guid SubmittedById { get; set; }
    public string? SubmittedByName { get; set; }
    public Dictionary<string, object?> Data { get; set; } = new();
    public SubmissionStatus Status { get; set; }
    public string? ReviewNotes { get; set; }
    public Guid? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
