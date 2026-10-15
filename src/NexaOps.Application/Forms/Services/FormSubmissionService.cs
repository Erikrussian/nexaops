using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NexaOps.Application.Common.Exceptions;
using NexaOps.Application.Common.Interfaces;
using NexaOps.Application.Forms.DTOs;
using NexaOps.Application.Notifications.DTOs;
using NexaOps.Application.Notifications.Services;
using NexaOps.Application.Workflows.DTOs;
using NexaOps.Domain.Entities;
using NexaOps.Domain.Enums;

namespace NexaOps.Application.Forms.Services;

public interface IFormSubmissionService
{
    Task<FormSubmissionDto> SubmitAsync(Guid companyId, Guid formId, SubmitFormDto dto, CancellationToken cancellationToken = default);
    Task<FormSubmissionDto> GetByIdAsync(Guid companyId, Guid submissionId, CancellationToken cancellationToken = default);
    Task<List<FormSubmissionDto>> ListByFormAsync(Guid companyId, Guid formId, SubmissionStatus? status = null, CancellationToken cancellationToken = default);
    Task<List<FormSubmissionDto>> ListByCompanyAsync(Guid companyId, SubmissionStatus? status = null, CancellationToken cancellationToken = default);
    Task<FormSubmissionDto> ReviewAsync(Guid companyId, Guid submissionId, ReviewSubmissionDto dto, CancellationToken cancellationToken = default);
}

public class FormSubmissionService : IFormSubmissionService
{
    private readonly IApplicationDbContext _context;
    private readonly ICompanyAccessService _companyAccessService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFormValidatorService _formValidatorService;
    private readonly INotificationService _notificationService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public FormSubmissionService(
        IApplicationDbContext context,
        ICompanyAccessService companyAccessService,
        ICurrentUserService currentUserService,
        IFormValidatorService formValidatorService,
        INotificationService notificationService)
    {
        _context = context;
        _companyAccessService = companyAccessService;
        _currentUserService = currentUserService;
        _formValidatorService = formValidatorService;
        _notificationService = notificationService;
    }

    public async Task<FormSubmissionDto> SubmitAsync(Guid companyId, Guid formId, SubmitFormDto dto, CancellationToken cancellationToken = default)
    {
        await _companyAccessService.EnsureReadAccessAsync(companyId, cancellationToken);
        var currentUserId = _currentUserService.UserId!.Value;

        var form = await _context.FormDefinitions
            .FirstOrDefaultAsync(f => f.Id == formId && f.CompanyId == companyId, cancellationToken);

        if (form == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy biểu mẫu với ID: {formId}");
        }

        if (form.Status != FormStatus.Published)
        {
            throw new InvalidOperationException("Biểu mẫu chưa được kích hoạt để nhận bài nộp");
        }

        List<FormFieldDto> schemaFields;
        try
        {
            schemaFields = JsonSerializer.Deserialize<List<FormFieldDto>>(form.SchemaJson, JsonOptions) ?? new List<FormFieldDto>();
        }
        catch
        {
            schemaFields = new List<FormFieldDto>();
        }

        // Validate submission payload against schema
        _formValidatorService.ValidateSubmissionData(schemaFields, dto.Data ?? new Dictionary<string, object?>());

        // Check if form has an active workflow configured
        var workflow = await _context.ApprovalWorkflows
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.CompanyId == companyId && w.FormDefinitionId == formId && w.IsActive, cancellationToken);

        var activeSteps = workflow?.Steps
            .Where(s => s.IsActive)
            .OrderBy(s => s.OrderIndex)
            .ToList();

        var submission = new FormSubmission
        {
            Id = Guid.NewGuid(),
            FormDefinitionId = formId,
            CompanyId = companyId,
            SubmittedById = currentUserId,
            DataJson = JsonSerializer.Serialize(dto.Data ?? new Dictionary<string, object?>(), JsonOptions),
            Status = activeSteps != null && activeSteps.Count > 0 ? SubmissionStatus.InReview : SubmissionStatus.Pending,
            CurrentStepOrder = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Snapshot workflow steps if present
        if (activeSteps != null && activeSteps.Count > 0)
        {
            foreach (var step in activeSteps)
            {
                submission.ApprovalSteps.Add(new SubmissionApprovalStep
                {
                    Id = Guid.NewGuid(),
                    SubmissionId = submission.Id,
                    OrderIndex = step.OrderIndex,
                    StepName = step.Name,
                    StepType = step.StepType,
                    ApproverRole = step.ApproverRole,
                    SpecificApproverUserId = step.SpecificApproverUserId,
                    AllowSelfApproval = step.AllowSelfApproval,
                    Status = ApprovalStepStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        _context.FormSubmissions.Add(submission);
        await _context.SaveChangesAsync(cancellationToken);

        // Notify submitter
        await _notificationService.CreateNotificationAsync(new CreateNotificationDto
        {
            UserId = currentUserId,
            CompanyId = companyId,
            Type = NotificationType.FormSubmitted,
            Title = "Nộp biểu mẫu thành công",
            Message = $"Hồ sơ '{form.Title}' của bạn đã được ghi nhận thành công.",
            LinkUrl = $"/forms/submissions/{submission.Id}"
        }, cancellationToken);

        // If workflow exists, notify step 1 approver(s)
        if (activeSteps != null && activeSteps.Count > 0)
        {
            var firstStep = activeSteps[0];
            await NotifyStepApproversAsync(companyId, firstStep, form.Title, submission.Id, cancellationToken);
        }

        return await GetByIdAsync(companyId, submission.Id, cancellationToken);
    }

    public async Task<FormSubmissionDto> GetByIdAsync(Guid companyId, Guid submissionId, CancellationToken cancellationToken = default)
    {
        await _companyAccessService.EnsureReadAccessAsync(companyId, cancellationToken);
        var currentUserId = _currentUserService.UserId!.Value;

        var submission = await _context.FormSubmissions
            .Include(s => s.FormDefinition)
            .Include(s => s.SubmittedBy)
            .Include(s => s.ReviewedBy)
            .Include(s => s.ApprovalSteps)
                .ThenInclude(st => st.ReviewedBy)
            .Include(s => s.ApprovalSteps)
                .ThenInclude(st => st.SpecificApproverUser)
            .FirstOrDefaultAsync(s => s.Id == submissionId && s.CompanyId == companyId, cancellationToken);

        if (submission == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy bài nộp với ID: {submissionId}");
        }

        var isOwner = await _companyAccessService.IsOwnerAsync(companyId, cancellationToken);
        var userRole = await _companyAccessService.GetUserRoleInCompanyAsync(companyId, cancellationToken);

        var isElevated = isOwner || userRole is MemberRole.Admin or MemberRole.Manager;

        if (!isElevated && submission.SubmittedById != currentUserId)
        {
            throw new ForbiddenAccessException("Bạn không có quyền xem bài nộp của người khác");
        }

        return MapToDto(submission);
    }

    public async Task<List<FormSubmissionDto>> ListByFormAsync(Guid companyId, Guid formId, SubmissionStatus? status = null, CancellationToken cancellationToken = default)
    {
        await _companyAccessService.EnsureReadAccessAsync(companyId, cancellationToken);
        var currentUserId = _currentUserService.UserId!.Value;

        var isOwner = await _companyAccessService.IsOwnerAsync(companyId, cancellationToken);
        var userRole = await _companyAccessService.GetUserRoleInCompanyAsync(companyId, cancellationToken);
        var isElevated = isOwner || userRole is MemberRole.Admin or MemberRole.Manager;

        var query = _context.FormSubmissions
            .Include(s => s.FormDefinition)
            .Include(s => s.SubmittedBy)
            .Include(s => s.ReviewedBy)
            .Include(s => s.ApprovalSteps)
                .ThenInclude(st => st.ReviewedBy)
            .Include(s => s.ApprovalSteps)
                .ThenInclude(st => st.SpecificApproverUser)
            .Where(s => s.CompanyId == companyId && s.FormDefinitionId == formId);

        if (!isElevated)
        {
            query = query.Where(s => s.SubmittedById == currentUserId);
        }

        if (status.HasValue)
        {
            query = query.Where(s => s.Status == status.Value);
        }

        var submissions = await query
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return submissions.Select(MapToDto).ToList();
    }

    public async Task<List<FormSubmissionDto>> ListByCompanyAsync(Guid companyId, SubmissionStatus? status = null, CancellationToken cancellationToken = default)
    {
        await _companyAccessService.EnsureReadAccessAsync(companyId, cancellationToken);
        var currentUserId = _currentUserService.UserId!.Value;

        var isOwner = await _companyAccessService.IsOwnerAsync(companyId, cancellationToken);
        var userRole = await _companyAccessService.GetUserRoleInCompanyAsync(companyId, cancellationToken);
        var isElevated = isOwner || userRole is MemberRole.Admin or MemberRole.Manager;

        var query = _context.FormSubmissions
            .Include(s => s.FormDefinition)
            .Include(s => s.SubmittedBy)
            .Include(s => s.ReviewedBy)
            .Include(s => s.ApprovalSteps)
                .ThenInclude(st => st.ReviewedBy)
            .Include(s => s.ApprovalSteps)
                .ThenInclude(st => st.SpecificApproverUser)
            .Where(s => s.CompanyId == companyId);

        if (!isElevated)
        {
            query = query.Where(s => s.SubmittedById == currentUserId);
        }

        if (status.HasValue)
        {
            query = query.Where(s => s.Status == status.Value);
        }

        var submissions = await query
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return submissions.Select(MapToDto).ToList();
    }

    public async Task<FormSubmissionDto> ReviewAsync(Guid companyId, Guid submissionId, ReviewSubmissionDto dto, CancellationToken cancellationToken = default)
    {
        await _companyAccessService.EnsureCanReviewSubmissionsAsync(companyId, cancellationToken);
        var currentUserId = _currentUserService.UserId!.Value;

        var submission = await _context.FormSubmissions
            .Include(s => s.FormDefinition)
            .Include(s => s.ApprovalSteps)
            .FirstOrDefaultAsync(s => s.Id == submissionId && s.CompanyId == companyId, cancellationToken);

        if (submission == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy bài nộp với ID: {submissionId}");
        }

        if (dto.Status is not (SubmissionStatus.Approved or SubmissionStatus.Rejected))
        {
            throw new ArgumentException("Trạng thái duyệt phải là Approved hoặc Rejected", nameof(dto.Status));
        }

        submission.Status = dto.Status;
        submission.ReviewNotes = dto.Notes?.Trim();
        submission.ReviewedById = currentUserId;
        submission.ReviewedAt = DateTime.UtcNow;
        submission.UpdatedAt = DateTime.UtcNow;

        // If multi-step workflow snapshot exists, update steps accordingly for elevated review
        if (submission.ApprovalSteps != null && submission.ApprovalSteps.Count > 0)
        {
            var targetStatus = dto.Status == SubmissionStatus.Approved ? ApprovalStepStatus.Approved : ApprovalStepStatus.Rejected;
            foreach (var step in submission.ApprovalSteps)
            {
                if (step.Status == ApprovalStepStatus.Pending)
                {
                    step.Status = targetStatus;
                    step.ReviewedById = currentUserId;
                    step.ReviewedAt = DateTime.UtcNow;
                    step.ReviewNotes = dto.Notes?.Trim();
                    step.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Notify submitter of review decision
        await _notificationService.CreateNotificationAsync(new CreateNotificationDto
        {
            UserId = submission.SubmittedById,
            CompanyId = companyId,
            Type = dto.Status == SubmissionStatus.Approved ? NotificationType.SubmissionApproved : NotificationType.SubmissionRejected,
            Title = dto.Status == SubmissionStatus.Approved ? "Hồ sơ đã được phê duyệt" : "Hồ sơ bị từ chối",
            Message = $"Hồ sơ '{submission.FormDefinition?.Title}' của bạn đã được xử lý. Kết quả: {dto.Status}.",
            LinkUrl = $"/forms/submissions/{submission.Id}"
        }, cancellationToken);

        return await GetByIdAsync(companyId, submission.Id, cancellationToken);
    }

    private async Task NotifyStepApproversAsync(
        Guid companyId,
        ApprovalStep step,
        string formTitle,
        Guid submissionId,
        CancellationToken cancellationToken)
    {
        if (step.StepType == ApprovalStepType.SpecificUser && step.SpecificApproverUserId.HasValue)
        {
            await _notificationService.CreateNotificationAsync(new CreateNotificationDto
            {
                UserId = step.SpecificApproverUserId.Value,
                CompanyId = companyId,
                Type = NotificationType.ApprovalRequired,
                Title = "Yêu cầu phê duyệt hồ sơ mới",
                Message = $"Bạn có một hồ sơ mới '{formTitle}' cần phê duyệt tại bước '{step.Name}'.",
                LinkUrl = $"/forms/submissions/{submissionId}"
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
                    Message = $"Hồ sơ '{formTitle}' đang chờ Chủ sở hữu phê duyệt tại bước '{step.Name}'.",
                    LinkUrl = $"/forms/submissions/{submissionId}"
                }, cancellationToken);
            }
            return;
        }

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
                Title = "Yêu cầu phê duyệt hồ sơ mới",
                Message = $"Hồ sơ '{formTitle}' cần phê duyệt tại bước '{step.Name}'.",
                LinkUrl = $"/forms/submissions/{submissionId}"
            }, cancellationToken);
        }
    }

    private static FormSubmissionDto MapToDto(FormSubmission submission)
    {
        Dictionary<string, object?> data;
        try
        {
            data = JsonSerializer.Deserialize<Dictionary<string, object?>>(submission.DataJson, JsonOptions)
                   ?? new Dictionary<string, object?>();
        }
        catch
        {
            data = new Dictionary<string, object?>();
        }

        return new FormSubmissionDto
        {
            Id = submission.Id,
            FormDefinitionId = submission.FormDefinitionId,
            FormTitle = submission.FormDefinition?.Title ?? string.Empty,
            FormCode = submission.FormDefinition?.Code ?? string.Empty,
            CompanyId = submission.CompanyId,
            SubmittedById = submission.SubmittedById,
            SubmittedByName = submission.SubmittedBy?.Name ?? submission.SubmittedBy?.Email,
            Data = data,
            Status = submission.Status,
            CurrentStepOrder = submission.CurrentStepOrder,
            ReviewNotes = submission.ReviewNotes,
            ReviewedById = submission.ReviewedById,
            ReviewedByName = submission.ReviewedBy?.Name ?? submission.ReviewedBy?.Email,
            ReviewedAt = submission.ReviewedAt,
            CreatedAt = submission.CreatedAt,
            UpdatedAt = submission.UpdatedAt,
            ApprovalSteps = submission.ApprovalSteps
                .OrderBy(s => s.OrderIndex)
                .Select(s => new SubmissionApprovalStepDto
                {
                    Id = s.Id,
                    SubmissionId = s.SubmissionId,
                    OrderIndex = s.OrderIndex,
                    StepName = s.StepName,
                    StepType = s.StepType,
                    ApproverRole = s.ApproverRole,
                    SpecificApproverUserId = s.SpecificApproverUserId,
                    SpecificApproverUserName = s.SpecificApproverUser?.Name,
                    AllowSelfApproval = s.AllowSelfApproval,
                    Status = s.Status,
                    ReviewedById = s.ReviewedById,
                    ReviewedByName = s.ReviewedBy?.Name,
                    ReviewedAt = s.ReviewedAt,
                    ReviewNotes = s.ReviewNotes,
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt
                })
                .ToList()
        };
    }
}
