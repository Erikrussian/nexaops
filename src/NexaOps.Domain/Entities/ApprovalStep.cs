using NexaOps.Domain.Enums;

namespace NexaOps.Domain.Entities;

public class ApprovalStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkflowId { get; set; }
    public ApprovalWorkflow? Workflow { get; set; }
    public int OrderIndex { get; set; }
    public string Name { get; set; } = string.Empty;
    public ApprovalStepType StepType { get; set; } = ApprovalStepType.Role;
    public MemberRole? ApproverRole { get; set; }
    public Guid? SpecificApproverUserId { get; set; }
    public User? SpecificApproverUser { get; set; }
    public bool AllowSelfApproval { get; set; } = false;
    public int? TimeLimitHours { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
