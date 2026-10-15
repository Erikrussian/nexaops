using NexaOps.Domain.Enums;

namespace NexaOps.Application.Workflows.DTOs;

public class ApprovalStepDto
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public int OrderIndex { get; set; }
    public string Name { get; set; } = string.Empty;
    public ApprovalStepType StepType { get; set; }
    public MemberRole? ApproverRole { get; set; }
    public Guid? SpecificApproverUserId { get; set; }
    public string? SpecificApproverUserName { get; set; }
    public bool AllowSelfApproval { get; set; }
    public int? TimeLimitHours { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ApprovalWorkflowDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid FormDefinitionId { get; set; }
    public string FormTitle { get; set; } = string.Empty;
    public string FormCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ApprovalStepDto> Steps { get; set; } = new();
}

public class CreateOrUpdateApprovalStepDto
{
    public int OrderIndex { get; set; }
    public string Name { get; set; } = string.Empty;
    public ApprovalStepType StepType { get; set; }
    public MemberRole? ApproverRole { get; set; }
    public Guid? SpecificApproverUserId { get; set; }
    public bool AllowSelfApproval { get; set; } = false;
    public int? TimeLimitHours { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CreateOrUpdateWorkflowDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public List<CreateOrUpdateApprovalStepDto> Steps { get; set; } = new();
}

public class SubmissionApprovalStepDto
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public int OrderIndex { get; set; }
    public string StepName { get; set; } = string.Empty;
    public ApprovalStepType StepType { get; set; }
    public MemberRole? ApproverRole { get; set; }
    public Guid? SpecificApproverUserId { get; set; }
    public string? SpecificApproverUserName { get; set; }
    public bool AllowSelfApproval { get; set; }
    public ApprovalStepStatus Status { get; set; }
    public Guid? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class StepApprovalActionDto
{
    public ApprovalStepStatus Status { get; set; }
    public string? ReviewNotes { get; set; }
}
