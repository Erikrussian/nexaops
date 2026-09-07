using NexaOps.Domain.Enums;

namespace NexaOps.Domain.Entities;

public class FormSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FormDefinitionId { get; set; }
    public FormDefinition? FormDefinition { get; set; }
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    public Guid SubmittedById { get; set; }
    public User? SubmittedBy { get; set; }
    public string DataJson { get; set; } = "{}";
    public SubmissionStatus Status { get; set; } = SubmissionStatus.Pending;
    public string? ReviewNotes { get; set; }
    public Guid? ReviewedById { get; set; }
    public User? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
