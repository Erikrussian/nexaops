using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaOps.Application.Forms.DTOs;
using NexaOps.Application.Forms.Services;
using NexaOps.Domain.Enums;

namespace NexaOps.Api.Controllers;

[ApiController]
[Authorize]
public class FormsController : ControllerBase
{
    private readonly IFormService _formService;
    private readonly IFormSubmissionService _submissionService;
    private readonly IFormAnalyticsService _analyticsService;

    public FormsController(
        IFormService formService,
        IFormSubmissionService submissionService,
        IFormAnalyticsService analyticsService)
    {
        _formService = formService;
        _submissionService = submissionService;
        _analyticsService = analyticsService;
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
}
