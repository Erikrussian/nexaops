using NexaOps.Application.Workflows.DTOs;

namespace NexaOps.Application.Workflows.Services;

public interface IApprovalWorkflowService
{
    Task<ApprovalWorkflowDto?> GetWorkflowByFormAsync(Guid companyId, Guid formId, CancellationToken cancellationToken = default);
    Task<ApprovalWorkflowDto> CreateOrUpdateWorkflowAsync(Guid companyId, Guid formId, CreateOrUpdateWorkflowDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteWorkflowAsync(Guid companyId, Guid formId, CancellationToken cancellationToken = default);
    Task<List<SubmissionApprovalStepDto>> GetSubmissionApprovalTimelineAsync(Guid companyId, Guid submissionId, CancellationToken cancellationToken = default);
    Task<SubmissionApprovalStepDto> ProcessStepApprovalAsync(Guid companyId, Guid submissionId, int stepOrder, StepApprovalActionDto dto, CancellationToken cancellationToken = default);
    Task<SubmissionApprovalStepDto> DelegateStepAsync(Guid companyId, Guid submissionId, int stepOrder, DelegateStepDto dto, CancellationToken cancellationToken = default);
    Task<WorkflowAnalyticsDto> GetWorkflowAnalyticsAsync(Guid companyId, Guid formId, CancellationToken cancellationToken = default);
    Task<int> EscalateOverdueStepsAsync(Guid companyId, CancellationToken cancellationToken = default);
}
