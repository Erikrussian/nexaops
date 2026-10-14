using NexaOps.Domain.Enums;

namespace NexaOps.Domain.Entities;

public class SubmissionApprovalStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubmissionId { get; set; }
    public FormSubmission? Submission { get; set; }
    public int OrderIndex { get; set; }
    public string StepName { get; set; } = string.Empty;
    public ApprovalStepType StepType { get; set; }
    public MemberRole? ApproverRole { get; set; }
    public Guid? SpecificApproverUserId { get; set; }
    public User? SpecificApproverUser { get; set; }
    public bool AllowSelfApproval { get; set; }
    public ApprovalStepStatus Status { get; set; } = ApprovalStepStatus.Pending;
    public Guid? ReviewedById { get; set; }
    public User? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
