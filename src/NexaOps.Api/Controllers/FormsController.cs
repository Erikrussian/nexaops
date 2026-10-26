using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaOps.Application.Forms.DTOs;
using NexaOps.Application.Forms.Services;
using NexaOps.Application.Workflows.DTOs;
using NexaOps.Application.Workflows.Services;
using NexaOps.Domain.Enums;

namespace NexaOps.Api.Controllers;

[ApiController]
[Authorize]
public class FormsController : ControllerBase
{
    private readonly IFormService _formService;
    private readonly IFormSubmissionService _submissionService;
    private readonly IFormAnalyticsService _analyticsService;
    private readonly IApprovalWorkflowService _workflowService;

    public FormsController(
        IFormService formService,
        IFormSubmissionService submissionService,
        IFormAnalyticsService analyticsService,
        IApprovalWorkflowService workflowService)
    {
        _formService = formService;
        _submissionService = submissionService;
        _analyticsService = analyticsService;
        _workflowService = workflowService;
    }

    /// <summary>
    /// POST /companies/{companyId}/forms - Tạo biểu mẫu mới (Draft)
    /// </summary>
    [HttpPost("companies/{companyId:guid}/forms")]
    [ProducesResponseType(typeof(FormDefinitionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FormDefinitionDto>> Create(
        [FromRoute] Guid companyId,
        [FromBody] CreateFormDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _formService.CreateAsync(companyId, dto, cancellationToken);
        return CreatedAtAction(nameof(FindById), new { companyId, formId = result.Id }, result);
    }

    /// <summary>
    /// GET /companies/{companyId}/forms - Danh sách biểu mẫu theo công ty
    /// </summary>
    [HttpGet("companies/{companyId:guid}/forms")]
    [ProducesResponseType(typeof(List<FormDefinitionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<FormDefinitionDto>>> List(
        [FromRoute] Guid companyId,
        [FromQuery] Guid? departmentId = null,
        [FromQuery] FormStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _formService.ListAsync(companyId, departmentId, status, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /companies/{companyId}/forms/{formId} - Chi tiết biểu mẫu theo ID
    /// </summary>
    [HttpGet("companies/{companyId:guid}/forms/{formId:guid}")]
    [ProducesResponseType(typeof(FormDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FormDefinitionDto>> FindById(
        [FromRoute] Guid companyId,
        [FromRoute] Guid formId,
        CancellationToken cancellationToken)
    {
        var result = await _formService.GetByIdAsync(companyId, formId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /companies/{companyId}/forms/code/{code} - Chi tiết biểu mẫu theo mã code
    /// </summary>
    [HttpGet("companies/{companyId:guid}/forms/code/{code}")]
    [ProducesResponseType(typeof(FormDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FormDefinitionDto>> FindByCode(
        [FromRoute] Guid companyId,
        [FromRoute] string code,
        CancellationToken cancellationToken)
    {
        var result = await _formService.GetByCodeAsync(companyId, code, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// PUT /companies/{companyId}/forms/{formId} - Cập nhật biểu mẫu
    /// </summary>
    [HttpPut("companies/{companyId:guid}/forms/{formId:guid}")]
    [ProducesResponseType(typeof(FormDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FormDefinitionDto>> Update(
        [FromRoute] Guid companyId,
        [FromRoute] Guid formId,
        [FromBody] UpdateFormDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _formService.UpdateAsync(companyId, formId, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// PATCH /companies/{companyId}/forms/{formId}/status - Thay đổi trạng thái biểu mẫu (Draft, Published, Archived)
    /// </summary>
    [HttpPatch("companies/{companyId:guid}/forms/{formId:guid}/status")]
    [ProducesResponseType(typeof(FormDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FormDefinitionDto>> ChangeStatus(
        [FromRoute] Guid companyId,
        [FromRoute] Guid formId,
        [FromBody] ChangeFormStatusDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _formService.ChangeStatusAsync(companyId, formId, dto.Status, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// DELETE /companies/{companyId}/forms/{formId} - Xóa biểu mẫu (chỉ khi chưa có bài nộp)
    /// </summary>
    [HttpDelete("companies/{companyId:guid}/forms/{formId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(
        [FromRoute] Guid companyId,
        [FromRoute] Guid formId,
        CancellationToken cancellationToken)
    {
        await _formService.DeleteAsync(companyId, formId, cancellationToken);
        return Ok(new { message = "Đã xóa biểu mẫu thành công" });
    }

    // ==========================================
    // FORM SUBMISSIONS ENDPOINTS
    // ==========================================

    /// <summary>
    /// POST /companies/{companyId}/forms/{formId}/submissions - Nộp dữ liệu biểu mẫu
    /// </summary>
    [HttpPost("companies/{companyId:guid}/forms/{formId:guid}/submissions")]
    [ProducesResponseType(typeof(FormSubmissionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FormSubmissionDto>> Submit(
        [FromRoute] Guid companyId,
        [FromRoute] Guid formId,
        [FromBody] SubmitFormDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _submissionService.SubmitAsync(companyId, formId, dto, cancellationToken);
        return CreatedAtAction(nameof(FindSubmissionById), new { companyId, submissionId = result.Id }, result);
    }

    /// <summary>
    /// GET /companies/{companyId}/forms/{formId}/submissions - Danh sách bài nộp của một biểu mẫu
    /// </summary>
    [HttpGet("companies/{companyId:guid}/forms/{formId:guid}/submissions")]
    [ProducesResponseType(typeof(List<FormSubmissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<FormSubmissionDto>>> ListSubmissionsByForm(
        [FromRoute] Guid companyId,
        [FromRoute] Guid formId,
        [FromQuery] SubmissionStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _submissionService.ListByFormAsync(companyId, formId, status, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /companies/{companyId}/submissions - Danh sách tất cả bài nộp trong công ty
    /// </summary>
    [HttpGet("companies/{companyId:guid}/submissions")]
    [ProducesResponseType(typeof(List<FormSubmissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<FormSubmissionDto>>> ListCompanySubmissions(
        [FromRoute] Guid companyId,
        [FromQuery] SubmissionStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _submissionService.ListByCompanyAsync(companyId, status, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /companies/{companyId}/submissions/{submissionId} - Chi tiết bài nộp
    /// </summary>
    [HttpGet("companies/{companyId:guid}/submissions/{submissionId:guid}")]
    [ProducesResponseType(typeof(FormSubmissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FormSubmissionDto>> FindSubmissionById(
        [FromRoute] Guid companyId,
        [FromRoute] Guid submissionId,
        CancellationToken cancellationToken)
    {
        var result = await _submissionService.GetByIdAsync(companyId, submissionId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// POST /companies/{companyId}/submissions/{submissionId}/review - Phê duyệt hoặc từ chối bài nộp
    /// </summary>
    [HttpPost("companies/{companyId:guid}/submissions/{submissionId:guid}/review")]
    [ProducesResponseType(typeof(FormSubmissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FormSubmissionDto>> Review(
        [FromRoute] Guid companyId,
        [FromRoute] Guid submissionId,
        [FromBody] ReviewSubmissionDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _submissionService.ReviewAsync(companyId, submissionId, dto, cancellationToken);
        return Ok(result);
    }

    // ==========================================
    // FORM ANALYTICS ENDPOINTS
    // ==========================================

    /// <summary>
    /// GET /companies/{companyId}/forms/analytics/overview - Thống kê tổng quan biểu mẫu toàn công ty (Owner, Admin, Manager)
    /// </summary>
    [HttpGet("companies/{companyId:guid}/forms/analytics/overview")]
    [ProducesResponseType(typeof(CompanyFormAnalyticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CompanyFormAnalyticsDto>> GetCompanyOverview(
        [FromRoute] Guid companyId,
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        var result = await _analyticsService.GetCompanyOverviewAsync(companyId, days, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /companies/{companyId}/forms/{formId}/analytics - Thống kê chi tiết riêng cho một biểu mẫu (Owner, Admin, Manager)
    /// </summary>
    [HttpGet("companies/{companyId:guid}/forms/{formId:guid}/analytics")]
    [ProducesResponseType(typeof(FormDetailAnalyticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FormDetailAnalyticsDto>> GetFormAnalytics(
        [FromRoute] Guid companyId,
        [FromRoute] Guid formId,
        CancellationToken cancellationToken)
    {
        var result = await _analyticsService.GetFormAnalyticsAsync(companyId, formId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /companies/{companyId}/forms/{formId}/workflow - Lấy cấu hình quy trình phê duyệt của biểu mẫu
    /// </summary>
    [HttpGet("companies/{companyId:guid}/forms/{formId:guid}/workflow")]
    [ProducesResponseType(typeof(ApprovalWorkflowDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApprovalWorkflowDto>> GetWorkflow(
        [FromRoute] Guid companyId,
        [FromRoute] Guid formId,
        CancellationToken cancellationToken)
    {
        var result = await _workflowService.GetWorkflowByFormAsync(companyId, formId, cancellationToken);
        if (result == null) return NotFound(new ProblemDetails { Title = "Biểu mẫu chưa cấu hình quy trình phê duyệt" });
        return Ok(result);
    }

    /// <summary>
    /// POST /companies/{companyId}/forms/{formId}/workflow - Tạo hoặc cập nhật quy trình phê duyệt
    /// </summary>
    [HttpPost("companies/{companyId:guid}/forms/{formId:guid}/workflow")]
    [ProducesResponseType(typeof(ApprovalWorkflowDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApprovalWorkflowDto>> SaveWorkflow(
        [FromRoute] Guid companyId,
        [FromRoute] Guid formId,
        [FromBody] CreateOrUpdateWorkflowDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _workflowService.CreateOrUpdateWorkflowAsync(companyId, formId, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// DELETE /companies/{companyId}/forms/{formId}/workflow - Xóa quy trình phê duyệt của biểu mẫu
    /// </summary>
    [HttpDelete("companies/{companyId:guid}/forms/{formId:guid}/workflow")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteWorkflow(
        [FromRoute] Guid companyId,
        [FromRoute] Guid formId,
        CancellationToken cancellationToken)
    {
        await _workflowService.DeleteWorkflowAsync(companyId, formId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// GET /companies/{companyId}/submissions/{submissionId}/workflow-timeline - Lấy tiến trình các bước phê duyệt hồ sơ
    /// </summary>
    [HttpGet("companies/{companyId:guid}/submissions/{submissionId:guid}/workflow-timeline")]
    [ProducesResponseType(typeof(List<SubmissionApprovalStepDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<SubmissionApprovalStepDto>>> GetWorkflowTimeline(
        [FromRoute] Guid companyId,
        [FromRoute] Guid submissionId,
        CancellationToken cancellationToken)
    {
        var result = await _workflowService.GetSubmissionApprovalTimelineAsync(companyId, submissionId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// POST /companies/{companyId}/submissions/{submissionId}/steps/{stepOrder}/process - Phê duyệt hoặc từ chối một bước
    /// </summary>
    [HttpPost("companies/{companyId:guid}/submissions/{submissionId:guid}/steps/{stepOrder:int}/process")]
    [ProducesResponseType(typeof(SubmissionApprovalStepDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubmissionApprovalStepDto>> ProcessStepApproval(
        [FromRoute] Guid companyId,
        [FromRoute] Guid submissionId,
        [FromRoute] int stepOrder,
        [FromBody] StepApprovalActionDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _workflowService.ProcessStepApprovalAsync(companyId, submissionId, stepOrder, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// POST /companies/{companyId}/submissions/{submissionId}/steps/{stepOrder}/delegate - Ủy quyền phê duyệt bước cho thành viên khác
    /// </summary>
    [HttpPost("companies/{companyId:guid}/submissions/{submissionId:guid}/steps/{stepOrder:int}/delegate")]
    [ProducesResponseType(typeof(SubmissionApprovalStepDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubmissionApprovalStepDto>> DelegateStep(
        [FromRoute] Guid companyId,
        [FromRoute] Guid submissionId,
        [FromRoute] int stepOrder,
        [FromBody] DelegateStepDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _workflowService.DelegateStepAsync(companyId, submissionId, stepOrder, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /companies/{companyId}/forms/{formId}/workflow/analytics - Thống kê SLA và hiệu suất xử lý quy trình biểu mẫu
    /// </summary>
    [HttpGet("companies/{companyId:guid}/forms/{formId:guid}/workflow/analytics")]
    [ProducesResponseType(typeof(WorkflowAnalyticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkflowAnalyticsDto>> GetWorkflowAnalytics(
        [FromRoute] Guid companyId,
        [FromRoute] Guid formId,
        CancellationToken cancellationToken)
    {
        var result = await _workflowService.GetWorkflowAnalyticsAsync(companyId, formId, cancellationToken);
        return Ok(result);
    }
}

