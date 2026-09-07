using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NexaOps.Application.Common.Exceptions;
using NexaOps.Application.Common.Interfaces;
using NexaOps.Application.Forms.DTOs;
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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public FormSubmissionService(
        IApplicationDbContext context,
        ICompanyAccessService companyAccessService,
        ICurrentUserService currentUserService,
        IFormValidatorService formValidatorService)
    {
        _context = context;
        _companyAccessService = companyAccessService;
        _currentUserService = currentUserService;
        _formValidatorService = formValidatorService;
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

        var submission = new FormSubmission
        {
            Id = Guid.NewGuid(),
            FormDefinitionId = formId,
            CompanyId = companyId,
            SubmittedById = currentUserId,
            DataJson = JsonSerializer.Serialize(dto.Data ?? new Dictionary<string, object?>(), JsonOptions),
            Status = SubmissionStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.FormSubmissions.Add(submission);
        await _context.SaveChangesAsync(cancellationToken);

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

        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(companyId, submission.Id, cancellationToken);
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
            ReviewNotes = submission.ReviewNotes,
            ReviewedById = submission.ReviewedById,
            ReviewedByName = submission.ReviewedBy?.Name ?? submission.ReviewedBy?.Email,
            ReviewedAt = submission.ReviewedAt,
            CreatedAt = submission.CreatedAt,
            UpdatedAt = submission.UpdatedAt
        };
    }
}
