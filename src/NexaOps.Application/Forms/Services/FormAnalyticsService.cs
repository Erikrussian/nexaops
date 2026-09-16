using Microsoft.EntityFrameworkCore;
using NexaOps.Application.Common.Interfaces;
using NexaOps.Application.Forms.DTOs;
using NexaOps.Domain.Enums;

namespace NexaOps.Application.Forms.Services;

public interface IFormAnalyticsService
{
    Task<CompanyFormAnalyticsDto> GetCompanyOverviewAsync(Guid companyId, int days = 30, CancellationToken cancellationToken = default);
    Task<FormDetailAnalyticsDto> GetFormAnalyticsAsync(Guid companyId, Guid formId, CancellationToken cancellationToken = default);
}

public class FormAnalyticsService : IFormAnalyticsService
{
    private readonly IApplicationDbContext _context;
    private readonly ICompanyAccessService _companyAccessService;

    public FormAnalyticsService(
        IApplicationDbContext context,
        ICompanyAccessService companyAccessService)
    {
        _context = context;
        _companyAccessService = companyAccessService;
    }

    public async Task<CompanyFormAnalyticsDto> GetCompanyOverviewAsync(Guid companyId, int days = 30, CancellationToken cancellationToken = default)
    {
        await _companyAccessService.EnsureCanManageFormsAsync(companyId, cancellationToken);

        if (days < 1) days = 7;
        if (days > 90) days = 90;

        var forms = await _context.FormDefinitions
            .AsNoTracking()
            .Where(f => f.CompanyId == companyId)
            .Select(f => new { f.Id, f.Title, f.Code, f.Status })
            .ToListAsync(cancellationToken);

        var totalForms = forms.Count;
        var draftForms = forms.Count(f => f.Status == FormStatus.Draft);
        var publishedForms = forms.Count(f => f.Status == FormStatus.Published);
        var archivedForms = forms.Count(f => f.Status == FormStatus.Archived);

        var submissions = await _context.FormSubmissions
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId)
            .Select(s => new { s.Id, s.FormDefinitionId, s.Status, s.CreatedAt })
            .ToListAsync(cancellationToken);

        var totalSubmissions = submissions.Count;
        var pendingSubmissions = submissions.Count(s => s.Status == SubmissionStatus.Pending);
        var approvedSubmissions = submissions.Count(s => s.Status == SubmissionStatus.Approved);
        var rejectedSubmissions = submissions.Count(s => s.Status == SubmissionStatus.Rejected);

        var reviewedCount = approvedSubmissions + rejectedSubmissions;
        var approvalRate = reviewedCount > 0
            ? Math.Round((double)approvedSubmissions / reviewedCount * 100, 2)
            : 0;

        // Calculate daily submissions trend for the requested number of days
        var startDate = DateTime.UtcNow.Date.AddDays(-days + 1);
        var recentSubmissions = submissions.Where(s => s.CreatedAt.Date >= startDate).ToList();
        var groupedByDate = recentSubmissions
            .GroupBy(s => s.CreatedAt.ToString("yyyy-MM-dd"))
            .ToDictionary(g => g.Key, g => g.Count());

        var dailyMetrics = new List<DailySubmissionMetricDto>();
        for (var d = startDate; d <= DateTime.UtcNow.Date; d = d.AddDays(1))
        {
            var dateStr = d.ToString("yyyy-MM-dd");
            dailyMetrics.Add(new DailySubmissionMetricDto
            {
                Date = dateStr,
                Count = groupedByDate.TryGetValue(dateStr, out var c) ? c : 0
            });
        }

        // Top active forms
        var formsLookup = forms.ToDictionary(f => f.Id, f => f);
        var topForms = submissions
            .GroupBy(s => s.FormDefinitionId)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g =>
            {
                var form = formsLookup.TryGetValue(g.Key, out var f) ? f : null;
                return new FormUsageMetricDto
                {
                    FormId = g.Key,
                    Title = form?.Title ?? "Unknown Form",
                    Code = form?.Code ?? string.Empty,
                    SubmissionCount = g.Count()
                };
            })
            .ToList();

        return new CompanyFormAnalyticsDto
        {
            CompanyId = companyId,
            TotalForms = totalForms,
            DraftForms = draftForms,
            PublishedForms = publishedForms,
            ArchivedForms = archivedForms,
            TotalSubmissions = totalSubmissions,
            PendingSubmissions = pendingSubmissions,
            ApprovedSubmissions = approvedSubmissions,
            RejectedSubmissions = rejectedSubmissions,
            ApprovalRatePercentage = approvalRate,
            RecentDailySubmissions = dailyMetrics,
            TopActiveForms = topForms
        };
    }

    public async Task<FormDetailAnalyticsDto> GetFormAnalyticsAsync(Guid companyId, Guid formId, CancellationToken cancellationToken = default)
    {
        await _companyAccessService.EnsureCanManageFormsAsync(companyId, cancellationToken);

        var form = await _context.FormDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == formId && f.CompanyId == companyId, cancellationToken);

        if (form == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy biểu mẫu với ID: {formId}");
        }

        var submissions = await _context.FormSubmissions
            .AsNoTracking()
            .Where(s => s.FormDefinitionId == formId && s.CompanyId == companyId)
            .Select(s => new { s.Id, s.Status, s.CreatedAt, s.ReviewedAt })
            .ToListAsync(cancellationToken);

        var totalSubmissions = submissions.Count;
        var pendingCount = submissions.Count(s => s.Status == SubmissionStatus.Pending);
        var approvedCount = submissions.Count(s => s.Status == SubmissionStatus.Approved);
        var rejectedCount = submissions.Count(s => s.Status == SubmissionStatus.Rejected);

        var reviewedCount = approvedCount + rejectedCount;
        var approvalRate = reviewedCount > 0
            ? Math.Round((double)approvedCount / reviewedCount * 100, 2)
            : 0;

        // Calculate average review time in hours
        var reviewedSubmissions = submissions
            .Where(s => s.ReviewedAt.HasValue && s.ReviewedAt.Value >= s.CreatedAt)
            .ToList();

        double? avgReviewTimeHours = null;
        if (reviewedSubmissions.Count > 0)
        {
            var totalHours = reviewedSubmissions.Sum(s => (s.ReviewedAt!.Value - s.CreatedAt).TotalHours);
            avgReviewTimeHours = Math.Round(totalHours / reviewedSubmissions.Count, 2);
        }

        return new FormDetailAnalyticsDto
        {
            FormId = form.Id,
            FormTitle = form.Title,
            FormCode = form.Code,
            Version = form.Version,
            Status = form.Status,
            TotalSubmissions = totalSubmissions,
            PendingCount = pendingCount,
            ApprovedCount = approvedCount,
            RejectedCount = rejectedCount,
            ApprovalRatePercentage = approvalRate,
            AverageReviewTimeHours = avgReviewTimeHours
        };
    }
}
