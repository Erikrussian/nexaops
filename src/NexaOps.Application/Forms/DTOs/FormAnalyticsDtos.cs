using NexaOps.Domain.Enums;

namespace NexaOps.Application.Forms.DTOs;

public class DailySubmissionMetricDto
{
    public string Date { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class FormUsageMetricDto
{
    public Guid FormId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int SubmissionCount { get; set; }
}

public class CompanyFormAnalyticsDto
{
    public Guid CompanyId { get; set; }
    public int TotalForms { get; set; }
    public int DraftForms { get; set; }
    public int PublishedForms { get; set; }
    public int ArchivedForms { get; set; }
    public int TotalSubmissions { get; set; }
    public int PendingSubmissions { get; set; }
    public int ApprovedSubmissions { get; set; }
    public int RejectedSubmissions { get; set; }
    public double ApprovalRatePercentage { get; set; }
    public List<DailySubmissionMetricDto> RecentDailySubmissions { get; set; } = new();
    public List<FormUsageMetricDto> TopActiveForms { get; set; } = new();
}

public class FormDetailAnalyticsDto
{
    public Guid FormId { get; set; }
    public string FormTitle { get; set; } = string.Empty;
    public string FormCode { get; set; } = string.Empty;
    public int Version { get; set; }
    public FormStatus Status { get; set; }
    public int TotalSubmissions { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public double ApprovalRatePercentage { get; set; }
    public double? AverageReviewTimeHours { get; set; }
}
