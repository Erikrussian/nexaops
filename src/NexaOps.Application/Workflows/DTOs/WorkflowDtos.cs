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

public class DelegateStepDto
{
    public Guid NewApproverUserId { get; set; }
    public string? Reason { get; set; }
}

public class WorkflowStepMetricDto
{
    public int OrderIndex { get; set; }
    public string StepName { get; set; } = string.Empty;
    public int TotalSubmissionsProcessed { get; set; }
    public int PendingCount { get; set; }
    public double AverageReviewTimeHours { get; set; }
}

public class WorkflowAnalyticsDto
{
    public Guid WorkflowId { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public int TotalSubmissions { get; set; }
    public int FullyApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public int InReviewCount { get; set; }
    public double OverallAverageTurnaroundHours { get; set; }
    public List<WorkflowStepMetricDto> StepMetrics { get; set; } = new();
}
