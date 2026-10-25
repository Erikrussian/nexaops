using Microsoft.EntityFrameworkCore;
using NexaOps.Application.AuditLogs.Services;
using NexaOps.Application.Common.Exceptions;
using NexaOps.Application.Common.Interfaces;
using NexaOps.Application.Notifications.DTOs;
using NexaOps.Application.Notifications.Services;
using NexaOps.Application.Workflows.DTOs;
using NexaOps.Domain.Entities;
using NexaOps.Domain.Enums;

namespace NexaOps.Application.Workflows.Services;

public class ApprovalWorkflowService : IApprovalWorkflowService
{
    private readonly IApplicationDbContext _context;
    private readonly ICompanyAccessService _companyAccessService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly INotificationService _notificationService;
    private readonly IRealtimeNotificationService? _realtimeNotificationService;

    public ApprovalWorkflowService(
        IApplicationDbContext context,
        ICompanyAccessService companyAccessService,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        INotificationService notificationService,
        IRealtimeNotificationService? realtimeNotificationService = null)
    {
        _context = context;
        _companyAccessService = companyAccessService;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
        _realtimeNotificationService = realtimeNotificationService;
    }

    public async Task<ApprovalWorkflowDto?> GetWorkflowByFormAsync(Guid companyId, Guid formId, CancellationToken cancellationToken = default)
    {
        await _companyAccessService.EnsureReadAccessAsync(companyId, cancellationToken);

        var workflow = await _context.ApprovalWorkflows
            .Include(w => w.FormDefinition)
            .Include(w => w.Steps)
                .ThenInclude(s => s.SpecificApproverUser)
            .FirstOrDefaultAsync(w => w.CompanyId == companyId && w.FormDefinitionId == formId, cancellationToken);

        if (workflow == null) return null;

        return MapToDto(workflow);
    }

    public async Task<ApprovalWorkflowDto> CreateOrUpdateWorkflowAsync(
        Guid companyId,
        Guid formId,
        CreateOrUpdateWorkflowDto dto,
        CancellationToken cancellationToken = default)
    {
        await _companyAccessService.EnsureCanManageFormsAsync(companyId, cancellationToken);
        var currentUserId = _currentUserService.UserId!.Value;

        var form = await _context.FormDefinitions
            .FirstOrDefaultAsync(f => f.Id == formId && f.CompanyId == companyId, cancellationToken);

        if (form == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy biểu mẫu với ID: {formId}");
        }

        // Validate steps if workflow is active
        if (dto.IsActive && (dto.Steps == null || dto.Steps.Count == 0))
        {
            throw new ArgumentException("Quy trình phê duyệt kích hoạt phải có ít nhất 1 bước duyệt", nameof(dto.Steps));
        }

        if (dto.Steps != null && dto.Steps.Count > 0)
        {
            var orderedSteps = dto.Steps.OrderBy(s => s.OrderIndex).ToList();
            for (int i = 0; i < orderedSteps.Count; i++)
            {
                var expectedOrder = i + 1;
                if (orderedSteps[i].OrderIndex != expectedOrder)
                {
                    throw new ArgumentException($"Thứ tự bước duyệt phải liên tục từ 1 (bước thứ {i + 1} có thứ tự là {orderedSteps[i].OrderIndex})");
                }

                if (string.IsNullOrWhiteSpace(orderedSteps[i].Name))
                {
                    throw new ArgumentException($"Tên bước duyệt thứ {expectedOrder} không được để trống");
                }

                if (orderedSteps[i].StepType == ApprovalStepType.Role && !orderedSteps[i].ApproverRole.HasValue)
                {
                    throw new ArgumentException($"Bước duyệt thứ {expectedOrder} thuộc loại Role phải chỉ định vai trò phê duyệt");
                }

                if (orderedSteps[i].StepType == ApprovalStepType.SpecificUser)
                {
                    if (!orderedSteps[i].SpecificApproverUserId.HasValue)
                    {
                        throw new ArgumentException($"Bước duyệt thứ {expectedOrder} phải chỉ định người phê duyệt cụ thể");
                    }

                    var approverId = orderedSteps[i].SpecificApproverUserId!.Value;
                    var isMember = await _context.CompanyMembers
                        .AnyAsync(m => m.CompanyId == companyId && m.UserId == approverId, cancellationToken);

                    if (!isMember)
                    {
                        throw new ArgumentException($"Người phê duyệt được chỉ định ở bước {expectedOrder} không thuộc tổ chức này");
                    }
                }
            }
        }

        var workflow = await _context.ApprovalWorkflows
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.CompanyId == companyId && w.FormDefinitionId == formId, cancellationToken);

        bool isCreate = workflow == null;

        if (isCreate)
        {
            workflow = new ApprovalWorkflow
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                FormDefinitionId = formId,
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim(),
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.ApprovalWorkflows.Add(workflow);
        }
        else
        {
            workflow!.Name = dto.Name.Trim();
            workflow.Description = dto.Description?.Trim();
            workflow.IsActive = dto.IsActive;
            workflow.UpdatedAt = DateTime.UtcNow;

            // Remove previous steps and replace
            _context.ApprovalSteps.RemoveRange(workflow.Steps);
        }

        // Add new steps
        if (dto.Steps != null)
        {
            foreach (var stepDto in dto.Steps.OrderBy(s => s.OrderIndex))
            {
                var step = new ApprovalStep
                {
                    Id = Guid.NewGuid(),
                    WorkflowId = workflow.Id,
                    OrderIndex = stepDto.OrderIndex,
                    Name = stepDto.Name.Trim(),
                    StepType = stepDto.StepType,
                    ApproverRole = stepDto.ApproverRole,
                    SpecificApproverUserId = stepDto.SpecificApproverUserId,
                    AllowSelfApproval = stepDto.AllowSelfApproval,
                    TimeLimitHours = stepDto.TimeLimitHours,
                    IsActive = stepDto.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                workflow.Steps.Add(step);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Audit Log
        await _auditLogService.LogAsync(
            companyId: companyId,
            actorId: currentUserId,
            action: isCreate ? "CREATE_WORKFLOW" : "UPDATE_WORKFLOW",
            entityType: "ApprovalWorkflow",
            entityId: workflow.Id.ToString(),
            details: new { FormId = formId, FormCode = form.Code, StepCount = workflow.Steps.Count, IsActive = workflow.IsActive },
            cancellationToken: cancellationToken);

        return await GetWorkflowByFormAsync(companyId, formId, cancellationToken)
            ?? throw new InvalidOperationException("Không thể tải quy trình vừa lưu");
    }

    public async Task<bool> DeleteWorkflowAsync(Guid companyId, Guid formId, CancellationToken cancellationToken = default)
    {
        await _companyAccessService.EnsureCanManageFormsAsync(companyId, cancellationToken);
        var currentUserId = _currentUserService.UserId!.Value;

        var workflow = await _context.ApprovalWorkflows
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.CompanyId == companyId && w.FormDefinitionId == formId, cancellationToken);

        if (workflow == null) return false;

        _context.ApprovalWorkflows.Remove(workflow);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            companyId: companyId,
            actorId: currentUserId,
            action: "DELETE_WORKFLOW",
            entityType: "ApprovalWorkflow",
            entityId: workflow.Id.ToString(),
            details: new { FormId = formId },
            cancellationToken: cancellationToken);

        return true;
    }

    public async Task<List<SubmissionApprovalStepDto>> GetSubmissionApprovalTimelineAsync(
        Guid companyId,
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        await _companyAccessService.EnsureReadAccessAsync(companyId, cancellationToken);

        var steps = await _context.SubmissionApprovalSteps
            .Include(s => s.ReviewedBy)
            .Include(s => s.SpecificApproverUser)
            .Where(s => s.SubmissionId == submissionId)
            .OrderBy(s => s.OrderIndex)
            .ToListAsync(cancellationToken);

        return steps.Select(MapStepToDto).ToList();
    }

    public async Task<SubmissionApprovalStepDto> ProcessStepApprovalAsync(
        Guid companyId,
        Guid submissionId,
        int stepOrder,
        StepApprovalActionDto dto,
        CancellationToken cancellationToken = default)
    {
        await _companyAccessService.EnsureReadAccessAsync(companyId, cancellationToken);
        var currentUserId = _currentUserService.UserId!.Value;

        var submission = await _context.FormSubmissions
            .Include(s => s.FormDefinition)
            .Include(s => s.SubmittedBy)
            .Include(s => s.ApprovalSteps)
            .FirstOrDefaultAsync(s => s.Id == submissionId && s.CompanyId == companyId, cancellationToken);

        if (submission == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy bài nộp với ID: {submissionId}");
        }

        if (submission.Status is SubmissionStatus.Approved or SubmissionStatus.Rejected or SubmissionStatus.Cancelled)
        {
            throw new InvalidOperationException($"Hồ sơ đã ở trạng thái kết thúc ({submission.Status}), không thể phê duyệt thêm");
        }

        var step = submission.ApprovalSteps.FirstOrDefault(s => s.OrderIndex == stepOrder);
        if (step == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy bước duyệt thứ {stepOrder} trong hồ sơ này");
        }

        if (submission.CurrentStepOrder != stepOrder)
        {
            throw new InvalidOperationException($"Bước duyệt này chưa đến lượt (bước hiện tại cần duyệt là {submission.CurrentStepOrder})");
        }

        if (step.Status != ApprovalStepStatus.Pending)
        {
            throw new InvalidOperationException("Bước duyệt này đã được xử lý trước đó");
        }

        // Self approval policy check
        if (!step.AllowSelfApproval && submission.SubmittedById == currentUserId)
        {
            throw new ForbiddenAccessException("Chính sách quy trình không cho phép người nộp tự phê duyệt hồ sơ của mình");
        }

        // Verify reviewer eligibility
        await VerifyReviewerEligibilityAsync(companyId, currentUserId, step, submission, cancellationToken);

        var isOwner = await _companyAccessService.IsOwnerAsync(companyId, cancellationToken);
        var userRole = await _companyAccessService.GetUserRoleInCompanyAsync(companyId, cancellationToken);
        var reviewerName = (await _context.Users.FindAsync(new object[] { currentUserId }, cancellationToken))?.Name ?? "Người duyệt";

        if (dto.Status == ApprovalStepStatus.Approved)
        {
            step.Status = ApprovalStepStatus.Approved;
            step.ReviewedById = currentUserId;
            step.ReviewedAt = DateTime.UtcNow;
            step.ReviewNotes = dto.ReviewNotes?.Trim();
            step.UpdatedAt = DateTime.UtcNow;

            // Check if there is next step
            var nextStep = submission.ApprovalSteps.FirstOrDefault(s => s.OrderIndex == stepOrder + 1);
            if (nextStep != null)
            {
                submission.CurrentStepOrder = stepOrder + 1;
                submission.Status = SubmissionStatus.InReview;
                submission.UpdatedAt = DateTime.UtcNow;

                // Notify next step approver(s)
                await NotifyStepApproversAsync(companyId, nextStep, submission, cancellationToken);

                // Notify submitter that step was approved
                await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                {
                    UserId = submission.SubmittedById,
                    CompanyId = companyId,
                    Type = NotificationType.StepApproved,
                    Title = "Bước duyệt đã hoàn tất",
                    Message = $"Bước '{step.StepName}' của hồ sơ '{submission.FormDefinition?.Title}' đã được {reviewerName} phê duyệt.",
                    LinkUrl = $"/forms/submissions/{submission.Id}"
                }, cancellationToken);
            }
            else
            {
                // All steps completed!
                submission.Status = SubmissionStatus.Approved;
                submission.ReviewedById = currentUserId;
                submission.ReviewedAt = DateTime.UtcNow;
                submission.ReviewNotes = dto.ReviewNotes?.Trim();
                submission.UpdatedAt = DateTime.UtcNow;

                // Notify submitter of final approval
                await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                {
                    UserId = submission.SubmittedById,
                    CompanyId = companyId,
                    Type = NotificationType.SubmissionApproved,
                    Title = "Hồ sơ đã được phê duyệt toàn bộ",
                    Message = $"Chúc mừng! Hồ sơ '{submission.FormDefinition?.Title}' của bạn đã hoàn tất toàn bộ quy trình phê duyệt.",
                    LinkUrl = $"/forms/submissions/{submission.Id}"
                }, cancellationToken);
            }
        }
        else if (dto.Status == ApprovalStepStatus.Rejected)
        {
            step.Status = ApprovalStepStatus.Rejected;
            step.ReviewedById = currentUserId;
            step.ReviewedAt = DateTime.UtcNow;
            step.ReviewNotes = dto.ReviewNotes?.Trim();
            step.UpdatedAt = DateTime.UtcNow;

            // Terminate submission
            submission.Status = SubmissionStatus.Rejected;
            submission.ReviewedById = currentUserId;
            submission.ReviewedAt = DateTime.UtcNow;
            submission.ReviewNotes = dto.ReviewNotes?.Trim();
            submission.UpdatedAt = DateTime.UtcNow;

            // Mark subsequent steps as skipped
            foreach (var remaining in submission.ApprovalSteps.Where(s => s.OrderIndex > stepOrder))
            {
                remaining.Status = ApprovalStepStatus.Skipped;
                remaining.UpdatedAt = DateTime.UtcNow;
            }

            // Notify submitter of rejection
            await _notificationService.CreateNotificationAsync(new CreateNotificationDto
            {
                UserId = submission.SubmittedById,
                CompanyId = companyId,
                Type = NotificationType.SubmissionRejected,
                Title = "Hồ sơ bị từ chối",
                Message = $"Hồ sơ '{submission.FormDefinition?.Title}' đã bị từ chối tại bước '{step.StepName}' bởi {reviewerName}. Ghi chú: {dto.ReviewNotes ?? "Không có"}",
                LinkUrl = $"/forms/submissions/{submission.Id}"
            }, cancellationToken);
        }
        else
        {
            throw new ArgumentException("Trạng thái xử lý bước duyệt phải là Approved hoặc Rejected");
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Log audit
        await _auditLogService.LogAsync(
            companyId: companyId,
            actorId: currentUserId,
            action: dto.Status == ApprovalStepStatus.Approved ? "APPROVE_STEP" : "REJECT_STEP",
            entityType: "FormSubmission",
            entityId: submission.Id.ToString(),
            details: new { StepOrder = stepOrder, StepName = step.StepName, Status = step.Status.ToString(), Notes = step.ReviewNotes },
            cancellationToken: cancellationToken);

        if (_realtimeNotificationService != null)
        {
            await _realtimeNotificationService.BroadcastWorkflowEventAsync(
                companyId,
                "WorkflowStepUpdated",
                new { submissionId = submission.Id, stepOrder = stepOrder, status = (int)step.Status },
                cancellationToken);
        }

        return MapStepToDto(step);
    }

    private async Task VerifyReviewerEligibilityAsync(
        Guid companyId,
        Guid currentUserId,
        SubmissionApprovalStep step,
        FormSubmission submission,
        CancellationToken cancellationToken)
    {
        var isOwner = await _companyAccessService.IsOwnerAsync(companyId, cancellationToken);
        if (isOwner) return; // Company Owner has ultimate authority

        var userRole = await _companyAccessService.GetUserRoleInCompanyAsync(companyId, cancellationToken);
        if (userRole == MemberRole.Admin) return; // Company Admin has override authority

        switch (step.StepType)
        {
            case ApprovalStepType.CompanyOwner:
                if (!isOwner)
                {
                    throw new ForbiddenAccessException("Bước này chỉ dành riêng cho Chủ sở hữu công ty (Owner) phê duyệt");
                }
                break;

            case ApprovalStepType.SpecificUser:
                if (step.SpecificApproverUserId.HasValue && step.SpecificApproverUserId.Value != currentUserId)
                {
                    throw new ForbiddenAccessException("Bạn không phải người phê duyệt được chỉ định cho bước này");
                }
                break;

            case ApprovalStepType.Role:
                if (step.ApproverRole.HasValue)
                {
                    if (userRole == null || (int)userRole.Value > (int)step.ApproverRole.Value)
                    {
                        throw new ForbiddenAccessException($"Bước này yêu cầu vai trò tối thiểu là {step.ApproverRole.Value}");
                    }
                }
                break;

            case ApprovalStepType.DepartmentManager:
                // Check if user is manager of submitter's department
                var submitterMember = await _context.CompanyMembers
                    .Include(m => m.Department)
                    .FirstOrDefaultAsync(m => m.CompanyId == companyId && m.UserId == submission.SubmittedById, cancellationToken);

                Guid? deptId = submitterMember?.DepartmentId ?? submission.FormDefinition?.DepartmentId;
                if (deptId.HasValue)
                {
                    var dept = await _context.Departments.FirstOrDefaultAsync(d => d.Id == deptId.Value, cancellationToken);
                    if (dept != null && dept.ManagerId == currentUserId)
                    {
                        return; // Is manager of department
                    }
                }

                if (userRole != MemberRole.Manager && userRole != MemberRole.Admin && !isOwner)
                {
                    throw new ForbiddenAccessException("Bước này yêu cầu Trưởng phòng ban (Department Manager) phê duyệt");
                }
                break;
        }
    }

    private async Task NotifyStepApproversAsync(
        Guid companyId,
        SubmissionApprovalStep step,
        FormSubmission submission,
        CancellationToken cancellationToken)
    {
        var formTitle = submission.FormDefinition?.Title ?? "biểu mẫu";

        if (step.StepType == ApprovalStepType.SpecificUser && step.SpecificApproverUserId.HasValue)
        {
            await _notificationService.CreateNotificationAsync(new CreateNotificationDto
            {
                UserId = step.SpecificApproverUserId.Value,
                CompanyId = companyId,
                Type = NotificationType.ApprovalRequired,
                Title = "Yêu cầu phê duyệt hồ sơ mới",
                Message = $"Bạn có một hồ sơ mới '{formTitle}' cần phê duyệt tại bước '{step.StepName}'.",
                LinkUrl = $"/forms/submissions/{submission.Id}"
            }, cancellationToken);
            return;
        }

        if (step.StepType == ApprovalStepType.CompanyOwner)
        {
            var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == companyId, cancellationToken);
            if (company != null)
            {
                await _notificationService.CreateNotificationAsync(new CreateNotificationDto
                {
                    UserId = company.OwnerId,
                    CompanyId = companyId,
                    Type = NotificationType.ApprovalRequired,
                    Title = "Yêu cầu phê duyệt từ Chủ sở hữu",
                    Message = $"Hồ sơ '{formTitle}' đang chờ Chủ sở hữu phê duyệt tại bước '{step.StepName}'.",
                    LinkUrl = $"/forms/submissions/{submission.Id}"
                }, cancellationToken);
            }
            return;
        }

        // For Role or DepartmentManager, notify corresponding members
        var approverRole = step.ApproverRole ?? MemberRole.Manager;
        var membersToNotify = await _context.CompanyMembers
            .Where(m => m.CompanyId == companyId && m.UserId.HasValue && (int)m.Role <= (int)approverRole)
            .Select(m => m.UserId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var userId in membersToNotify)
        {
            await _notificationService.CreateNotificationAsync(new CreateNotificationDto
            {
                UserId = userId,
                CompanyId = companyId,
                Type = NotificationType.ApprovalRequired,
                Title = "Yêu cầu phê duyệt hồ sơ",
                Message = $"Hồ sơ '{formTitle}' cần phê duyệt tại bước '{step.StepName}'.",
                LinkUrl = $"/forms/submissions/{submission.Id}"
            }, cancellationToken);
        }
    }

    private static ApprovalWorkflowDto MapToDto(ApprovalWorkflow workflow)
    {
        return new ApprovalWorkflowDto
        {
            Id = workflow.Id,
            CompanyId = workflow.CompanyId,
            FormDefinitionId = workflow.FormDefinitionId,
            FormTitle = workflow.FormDefinition?.Title ?? string.Empty,
            FormCode = workflow.FormDefinition?.Code ?? string.Empty,
            Name = workflow.Name,
            Description = workflow.Description,
            IsActive = workflow.IsActive,
            CreatedAt = workflow.CreatedAt,
            UpdatedAt = workflow.UpdatedAt,
            Steps = workflow.Steps
                .OrderBy(s => s.OrderIndex)
                .Select(s => new ApprovalStepDto
                {
                    Id = s.Id,
                    WorkflowId = s.WorkflowId,
                    OrderIndex = s.OrderIndex,
                    Name = s.Name,
                    StepType = s.StepType,
                    ApproverRole = s.ApproverRole,
                    SpecificApproverUserId = s.SpecificApproverUserId,
                    SpecificApproverUserName = s.SpecificApproverUser?.Name,
                    AllowSelfApproval = s.AllowSelfApproval,
                    TimeLimitHours = s.TimeLimitHours,
                    IsActive = s.IsActive,
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt
                })
                .ToList()
        };
    }

    private static SubmissionApprovalStepDto MapStepToDto(SubmissionApprovalStep step)
    {
        return new SubmissionApprovalStepDto
        {
            Id = step.Id,
            SubmissionId = step.SubmissionId,
            OrderIndex = step.OrderIndex,
            StepName = step.StepName,
            StepType = step.StepType,
            ApproverRole = step.ApproverRole,
            SpecificApproverUserId = step.SpecificApproverUserId,
            SpecificApproverUserName = step.SpecificApproverUser?.Name,
            AllowSelfApproval = step.AllowSelfApproval,
            Status = step.Status,
            ReviewedById = step.ReviewedById,
            ReviewedByName = step.ReviewedBy?.Name,
            ReviewedAt = step.ReviewedAt,
            ReviewNotes = step.ReviewNotes,
            CreatedAt = step.CreatedAt,
            UpdatedAt = step.UpdatedAt
        };
    }
}
