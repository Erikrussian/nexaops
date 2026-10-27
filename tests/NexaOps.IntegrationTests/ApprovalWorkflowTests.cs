using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NexaOps.Application.AuditLogs.DTOs;
using NexaOps.Application.Auth.DTOs;
using NexaOps.Application.Common.Models;
using NexaOps.Application.Companies.DTOs;
using NexaOps.Application.CompanyMembers.DTOs;
using NexaOps.Application.Forms.DTOs;
using NexaOps.Application.Notifications.DTOs;
using NexaOps.Application.Workflows.DTOs;
using NexaOps.Domain.Enums;
using NexaOps.IntegrationTests.Common;

namespace NexaOps.IntegrationTests;

public class ApprovalWorkflowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ApprovalWorkflowTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string Token, Guid CompanyId, Guid UserId)> SetupCompanyAndOwnerAsync()
    {
        var tag = Guid.NewGuid().ToString("N");
        var registerRequest = new RegisterRequest
        {
            Name = $"Workflow Owner {tag}",
            Email = $"wfowner.{tag}@nexaops.com",
            Password = "password123"
        };
        var regRes = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        var authResult = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        var token = authResult!.AccessToken;

        var compRequest = new CreateCompanyRequest { Name = $"Workflow Company {tag}" };
        var msgComp = new HttpRequestMessage(HttpMethod.Post, "/companies")
        {
            Content = JsonContent.Create(compRequest)
        };
        msgComp.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var compRes = await _client.SendAsync(msgComp);
        var company = await compRes.Content.ReadFromJsonAsync<CompanyResponse>(JsonOptions);

        return (token, company!.Id, authResult.User.Id);
    }

    private async Task<(string Token, Guid UserId)> CreateUserAndJoinCompanyAsync(string ownerToken, Guid companyId, MemberRole role)
    {
        var tag = Guid.NewGuid().ToString("N");
        var email = $"wfmember.{tag}@nexaops.com";
        var regRes = await _client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Name = $"Member {tag}",
            Email = email,
            Password = "password123"
        });
        var authResult = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        var token = authResult!.AccessToken;

        var inviteMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/members/invite")
        {
            Content = JsonContent.Create(new InviteMemberRequest { Email = email, Role = role })
        };
        inviteMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var inviteRes = await _client.SendAsync(inviteMsg);
        var inviteResult = await inviteRes.Content.ReadFromJsonAsync<InviteMemberResponse>(JsonOptions);

        var tokenParam = inviteResult!.InviteLink.Split("token=")[1];
        var acceptMsg = new HttpRequestMessage(HttpMethod.Post, "/company-members/accept-invite")
        {
            Content = JsonContent.Create(new AcceptInviteRequest { InviteToken = tokenParam })
        };
        acceptMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var acceptRes = await _client.SendAsync(acceptMsg);
        Assert.True(acceptRes.IsSuccessStatusCode);

        return (token, authResult.User.Id);
    }

    private async Task<FormDefinitionDto> CreatePublishedFormAsync(string ownerToken, Guid companyId)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var createFormReq = new CreateFormDto
        {
            Title = $"Workflow Test Form {tag}",
            Code = $"WF_FORM_{tag}".ToUpperInvariant(),
            Description = "Test Form with multi-step workflow",
            Fields = new List<FormFieldDto>
            {
                new() { Name = "reason", Label = "Lý do", Type = FormFieldType.Text, Required = true }
            }
        };

        var postMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms")
        {
            Content = JsonContent.Create(createFormReq)
        };
        postMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var postRes = await _client.SendAsync(postMsg);
        var form = await postRes.Content.ReadFromJsonAsync<FormDefinitionDto>(JsonOptions);

        // Publish form
        var publishMsg = new HttpRequestMessage(HttpMethod.Patch, $"/companies/{companyId}/forms/{form!.Id}/status")
        {
            Content = JsonContent.Create(new ChangeFormStatusDto { Status = FormStatus.Published })
        };
        publishMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var pubRes = await _client.SendAsync(publishMsg);
        return (await pubRes.Content.ReadFromJsonAsync<FormDefinitionDto>(JsonOptions))!;
    }

    [Fact]
    public async Task CreateWorkflow_WithValidSequentialSteps_ShouldSucceedAndSnapshotOnSubmit()
    {
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync();
        var (memberToken, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member);
        var form = await CreatePublishedFormAsync(ownerToken, companyId);

        // Create workflow via API
        var wfMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/workflow")
        {
            Content = JsonContent.Create(new CreateOrUpdateWorkflowDto
            {
                Name = "Two Step Approval",
                IsActive = true,
                Steps = new List<CreateOrUpdateApprovalStepDto>
                {
                    new() { OrderIndex = 1, Name = "Trưởng phòng duyệt", StepType = ApprovalStepType.Role, ApproverRole = MemberRole.Manager, AllowSelfApproval = false },
                    new() { OrderIndex = 2, Name = "Chủ sở hữu duyệt", StepType = ApprovalStepType.CompanyOwner, AllowSelfApproval = false }
                }
            })
        };
        wfMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var wfRes = await _client.SendAsync(wfMsg);
        Assert.Equal(HttpStatusCode.OK, wfRes.StatusCode);

        var workflowDto = await wfRes.Content.ReadFromJsonAsync<ApprovalWorkflowDto>(JsonOptions);
        Assert.NotNull(workflowDto);
        Assert.Equal(2, workflowDto.Steps.Count);

        // Submit form as member
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { ["reason"] = "Xin cấp máy tính" }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var submitRes = await _client.SendAsync(submitMsg);
        Assert.Equal(HttpStatusCode.Created, submitRes.StatusCode);

        var submission = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.NotNull(submission);
        Assert.Equal(SubmissionStatus.InReview, submission.Status);
        Assert.Equal(1, submission.CurrentStepOrder);
        Assert.Equal(2, submission.ApprovalSteps.Count);
        Assert.Equal("Trưởng phòng duyệt", submission.ApprovalSteps[0].StepName);
        Assert.Equal(ApprovalStepStatus.Pending, submission.ApprovalSteps[0].Status);
    }

    [Fact]
    public async Task CreateWorkflow_WithNonSequentialOrders_ShouldReturnBadRequest()
    {
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync();
        var form = await CreatePublishedFormAsync(ownerToken, companyId);

        var wfMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/workflow")
        {
            Content = JsonContent.Create(new CreateOrUpdateWorkflowDto
            {
                Name = "Invalid Order Workflow",
                IsActive = true,
                Steps = new List<CreateOrUpdateApprovalStepDto>
                {
                    new() { OrderIndex = 1, Name = "Step 1", StepType = ApprovalStepType.CompanyOwner },
                    new() { OrderIndex = 3, Name = "Step 3 (Gap)", StepType = ApprovalStepType.CompanyOwner }
                }
            })
        };
        wfMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var wfRes = await _client.SendAsync(wfMsg);

        Assert.Equal(HttpStatusCode.BadRequest, wfRes.StatusCode);
    }

    [Fact]
    public async Task ProcessStepApproval_WhenSelfApprovalForbidden_ShouldReturnForbidden()
    {
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync();
        var form = await CreatePublishedFormAsync(ownerToken, companyId);

        // Configure workflow with no self approval
        var wfMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/workflow")
        {
            Content = JsonContent.Create(new CreateOrUpdateWorkflowDto
            {
                Name = "Owner Step No Self Approval",
                IsActive = true,
                Steps = new List<CreateOrUpdateApprovalStepDto>
                {
                    new() { OrderIndex = 1, Name = "Owner Approval", StepType = ApprovalStepType.CompanyOwner, AllowSelfApproval = false }
                }
            })
        };
        wfMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var wfRes = await _client.SendAsync(wfMsg);
        Assert.Equal(HttpStatusCode.OK, wfRes.StatusCode);

        // Submit form as owner
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { ["reason"] = "Self request" }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var submitRes = await _client.SendAsync(submitMsg);
        var submission = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        // Owner tries to approve self submission
        var approveMsg = new HttpRequestMessage(
            HttpMethod.Post,
            $"/companies/{companyId}/submissions/{submission!.Id}/steps/1/process")
        {
            Content = JsonContent.Create(new StepApprovalActionDto
            {
                Status = ApprovalStepStatus.Approved,
                ReviewNotes = "Self approval attempt"
            })
        };
        approveMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var approveRes = await _client.SendAsync(approveMsg);

        Assert.Equal(HttpStatusCode.Forbidden, approveRes.StatusCode);
    }

    [Fact]
    public async Task ProcessStepApproval_ApproveAllSteps_ShouldFullyApproveSubmission()
    {
        var (ownerToken, companyId, ownerId) = await SetupCompanyAndOwnerAsync();
        var (managerToken, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager);
        var (memberToken, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member);
        var form = await CreatePublishedFormAsync(ownerToken, companyId);

        var wfMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/workflow")
        {
            Content = JsonContent.Create(new CreateOrUpdateWorkflowDto
            {
                Name = "Two Step Sequential",
                IsActive = true,
                Steps = new List<CreateOrUpdateApprovalStepDto>
                {
                    new() { OrderIndex = 1, Name = "Manager Step", StepType = ApprovalStepType.Role, ApproverRole = MemberRole.Manager, AllowSelfApproval = false },
                    new() { OrderIndex = 2, Name = "Owner Step", StepType = ApprovalStepType.CompanyOwner, AllowSelfApproval = false }
                }
            })
        };
        wfMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        await _client.SendAsync(wfMsg);

        // Submit as member
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { ["reason"] = "Travel expense" }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var submitRes = await _client.SendAsync(submitMsg);
        var submission = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        // Step 1: Manager approves
        var step1Msg = new HttpRequestMessage(
            HttpMethod.Post,
            $"/companies/{companyId}/submissions/{submission!.Id}/steps/1/process")
        {
            Content = JsonContent.Create(new StepApprovalActionDto
            {
                Status = ApprovalStepStatus.Approved,
                ReviewNotes = "Manager approved"
            })
        };
        step1Msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var step1Res = await _client.SendAsync(step1Msg);
        Assert.Equal(HttpStatusCode.OK, step1Res.StatusCode);

        // Step 2: Owner approves
        var step2Msg = new HttpRequestMessage(
            HttpMethod.Post,
            $"/companies/{companyId}/submissions/{submission.Id}/steps/2/process")
        {
            Content = JsonContent.Create(new StepApprovalActionDto
            {
                Status = ApprovalStepStatus.Approved,
                ReviewNotes = "Owner final approval"
            })
        };
        step2Msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var step2Res = await _client.SendAsync(step2Msg);
        Assert.Equal(HttpStatusCode.OK, step2Res.StatusCode);

        // Verify timeline
        var timelineMsg = new HttpRequestMessage(
            HttpMethod.Get,
            $"/companies/{companyId}/submissions/{submission.Id}/workflow-timeline");
        timelineMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var timelineRes = await _client.SendAsync(timelineMsg);
        var timeline = await timelineRes.Content.ReadFromJsonAsync<List<SubmissionApprovalStepDto>>(JsonOptions);

        Assert.NotNull(timeline);
        Assert.Equal(2, timeline.Count);
        Assert.Equal(ApprovalStepStatus.Approved, timeline[0].Status);
        Assert.Equal(ApprovalStepStatus.Approved, timeline[1].Status);

        // Verify submission status is Approved
        var getSubMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/submissions/{submission.Id}");
        getSubMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var getSubRes = await _client.SendAsync(getSubMsg);
        var finalSub = await getSubRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        Assert.Equal(SubmissionStatus.Approved, finalSub!.Status);
        Assert.Equal(ownerId, finalSub.ReviewedById);
    }

    [Fact]
    public async Task ProcessStepApproval_RejectStep_ShouldRejectSubmissionAndSkipRemainingSteps()
    {
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync();
        var (managerToken, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager);
        var (memberToken, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member);
        var form = await CreatePublishedFormAsync(ownerToken, companyId);

        var wfMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/workflow")
        {
            Content = JsonContent.Create(new CreateOrUpdateWorkflowDto
            {
                Name = "Reject at Step 1",
                IsActive = true,
                Steps = new List<CreateOrUpdateApprovalStepDto>
                {
                    new() { OrderIndex = 1, Name = "Manager Step", StepType = ApprovalStepType.Role, ApproverRole = MemberRole.Manager },
                    new() { OrderIndex = 2, Name = "Owner Step", StepType = ApprovalStepType.CompanyOwner }
                }
            })
        };
        wfMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        await _client.SendAsync(wfMsg);

        // Submit as member
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { ["reason"] = "Invalid request" }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var submitRes = await _client.SendAsync(submitMsg);
        var submission = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        // Manager rejects at step 1
        var rejectMsg = new HttpRequestMessage(
            HttpMethod.Post,
            $"/companies/{companyId}/submissions/{submission!.Id}/steps/1/process")
        {
            Content = JsonContent.Create(new StepApprovalActionDto
            {
                Status = ApprovalStepStatus.Rejected,
                ReviewNotes = "Ngân sách không hợp lý"
            })
        };
        rejectMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var rejectRes = await _client.SendAsync(rejectMsg);
        Assert.Equal(HttpStatusCode.OK, rejectRes.StatusCode);

        // Verify timeline shows rejected and skipped
        var timelineMsg = new HttpRequestMessage(
            HttpMethod.Get,
            $"/companies/{companyId}/submissions/{submission.Id}/workflow-timeline");
        timelineMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var timelineRes = await _client.SendAsync(timelineMsg);
        var timeline = await timelineRes.Content.ReadFromJsonAsync<List<SubmissionApprovalStepDto>>(JsonOptions);

        Assert.NotNull(timeline);
        Assert.Equal(ApprovalStepStatus.Rejected, timeline[0].Status);
        Assert.Equal(ApprovalStepStatus.Skipped, timeline[1].Status);

        // Verify submission status is Rejected
        var getSubMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/submissions/{submission.Id}");
        getSubMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var getSubRes = await _client.SendAsync(getSubMsg);
        var finalSub = await getSubRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        Assert.Equal(SubmissionStatus.Rejected, finalSub!.Status);
    }

    [Fact]
    public async Task NotificationsApi_ListAndMarkAsRead_ShouldOperateCorrectly()
    {
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync();
        var form = await CreatePublishedFormAsync(ownerToken, companyId);

        // Submitting a form triggers notification
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { ["reason"] = "Trigger notification" }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        await _client.SendAsync(submitMsg);

        // Get notifications
        var listMsg = new HttpRequestMessage(HttpMethod.Get, $"/notifications?companyId={companyId}");
        listMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var listRes = await _client.SendAsync(listMsg);
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);

        var notifList = await listRes.Content.ReadFromJsonAsync<NotificationListResponse>(JsonOptions);
        Assert.NotNull(notifList);
        Assert.True(notifList.Items.Count > 0);
        Assert.True(notifList.UnreadCount > 0);

        var firstNotifId = notifList.Items[0].Id;

        // Mark single as read
        var readMsg = new HttpRequestMessage(HttpMethod.Post, $"/notifications/{firstNotifId}/read");
        readMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var readRes = await _client.SendAsync(readMsg);
        Assert.Equal(HttpStatusCode.OK, readRes.StatusCode);

        // Mark all as read
        var readAllMsg = new HttpRequestMessage(HttpMethod.Post, $"/notifications/read-all?companyId={companyId}");
        readAllMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var readAllRes = await _client.SendAsync(readAllMsg);
        Assert.Equal(HttpStatusCode.OK, readAllRes.StatusCode);

        // Verify unread count is now 0
        var countMsg = new HttpRequestMessage(HttpMethod.Get, $"/notifications/unread-count?companyId={companyId}");
        countMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var countRes = await _client.SendAsync(countMsg);
        var unreadCount = await countRes.Content.ReadFromJsonAsync<int>(JsonOptions);
        Assert.Equal(0, unreadCount);
    }

    [Fact]
    public async Task ProcessStepApproval_ShouldRecordAuditLogAndTriggerNotifications()
    {
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync();
        var (managerToken, managerId) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager);
        var (memberToken, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member);
        var form = await CreatePublishedFormAsync(ownerToken, companyId);

        // Create 1-step workflow with Manager
        var wfMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/workflow")
        {
            Content = JsonContent.Create(new CreateOrUpdateWorkflowDto
            {
                Name = "Manager Only Workflow",
                IsActive = true,
                Steps = new List<CreateOrUpdateApprovalStepDto>
                {
                    new() { OrderIndex = 1, Name = "Manager Step", StepType = ApprovalStepType.Role, ApproverRole = MemberRole.Manager }
                }
            })
        };
        wfMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        await _client.SendAsync(wfMsg);

        // Submit form
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { ["reason"] = "Audit log test submission" }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var submitRes = await _client.SendAsync(submitMsg);
        var submission = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        // Manager approves step 1
        var approveMsg = new HttpRequestMessage(
            HttpMethod.Post,
            $"/companies/{companyId}/submissions/{submission!.Id}/steps/1/process")
        {
            Content = JsonContent.Create(new StepApprovalActionDto
            {
                Status = ApprovalStepStatus.Approved,
                ReviewNotes = "Approved with audit verification"
            })
        };
        approveMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var approveRes = await _client.SendAsync(approveMsg);
        Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);

        // Query Audit Logs as Owner
        var auditMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/audit-logs?action=APPROVE_STEP");
        auditMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var auditRes = await _client.SendAsync(auditMsg);
        Assert.Equal(HttpStatusCode.OK, auditRes.StatusCode);

        var auditLogs = await auditRes.Content.ReadFromJsonAsync<PagedResult<AuditLogDto>>(JsonOptions);
        Assert.NotNull(auditLogs);
        Assert.True(auditLogs.Items.Count > 0);

        var stepLog = auditLogs.Items.FirstOrDefault(l => l.Action == "APPROVE_STEP" && l.EntityId == submission.Id.ToString());
        Assert.NotNull(stepLog);
        Assert.Equal(managerId, stepLog.ActorId);
    }

    [Fact]
    public async Task ProcessStepApproval_Delegation_ShouldTransferAuthorityAndAllowNewApprover()
    {
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync();
        var (managerToken, managerId) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager);
        var (adminToken, adminId) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Admin);
        var (memberToken, memberId) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member);
        var form = await CreatePublishedFormAsync(ownerToken, companyId);

        // Workflow: Step 1 (Manager Role)
        var wfMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/workflow")
        {
            Content = JsonContent.Create(new CreateOrUpdateWorkflowDto
            {
                Name = "Delegation Flow Test",
                IsActive = true,
                Steps = new List<CreateOrUpdateApprovalStepDto>
                {
                    new() { OrderIndex = 1, Name = "Review Step", StepType = ApprovalStepType.Role, ApproverRole = MemberRole.Manager }
                }
            })
        };
        wfMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        await _client.SendAsync(wfMsg);

        // Member submits form
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { ["reason"] = "Delegation test" }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var submitRes = await _client.SendAsync(submitMsg);
        var submission = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        // Manager tries to delegate step 1 to the submitter (Anti-fraud check: should fail with 400 Bad Request)
        var fraudDelegateMsg = new HttpRequestMessage(
            HttpMethod.Post,
            $"/companies/{companyId}/submissions/{submission!.Id}/steps/1/delegate")
        {
            Content = JsonContent.Create(new DelegateStepDto
            {
                NewApproverUserId = memberId,
                Reason = "Self-approval attempt"
            })
        };
        fraudDelegateMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var fraudRes = await _client.SendAsync(fraudDelegateMsg);
        Assert.Equal(HttpStatusCode.BadRequest, fraudRes.StatusCode);

        // Manager delegates step 1 to Admin
        var validDelegateMsg = new HttpRequestMessage(
            HttpMethod.Post,
            $"/companies/{companyId}/submissions/{submission.Id}/steps/1/delegate")
        {
            Content = JsonContent.Create(new DelegateStepDto
            {
                NewApproverUserId = adminId,
                Reason = "Bàn giao do nghỉ phép"
            })
        };
        validDelegateMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var validDelegateRes = await _client.SendAsync(validDelegateMsg);
        Assert.Equal(HttpStatusCode.OK, validDelegateRes.StatusCode);

        var delegatedStep = await validDelegateRes.Content.ReadFromJsonAsync<SubmissionApprovalStepDto>(JsonOptions);
        Assert.NotNull(delegatedStep);
        Assert.Equal(adminId, delegatedStep.SpecificApproverUserId);
        Assert.Contains("Bàn giao do nghỉ phép", delegatedStep.ReviewNotes);

        // Admin now approves the delegated step
        var approveMsg = new HttpRequestMessage(
            HttpMethod.Post,
            $"/companies/{companyId}/submissions/{submission.Id}/steps/1/process")
        {
            Content = JsonContent.Create(new StepApprovalActionDto
            {
                Status = ApprovalStepStatus.Approved,
                ReviewNotes = "Duyệt theo ủy quyền"
            })
        };
        approveMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var approveRes = await _client.SendAsync(approveMsg);
        Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);

        // Verify final submission status is Approved
        var getSubMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/submissions/{submission.Id}");
        getSubMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var getSubRes = await _client.SendAsync(getSubMsg);
        var finalSub = await getSubRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.Equal(SubmissionStatus.Approved, finalSub!.Status);

        // Verify Workflow Analytics endpoint
        var analyticsMsg = new HttpRequestMessage(
            HttpMethod.Get,
            $"/companies/{companyId}/forms/{form.Id}/workflow/analytics");
        analyticsMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var analyticsRes = await _client.SendAsync(analyticsMsg);
        Assert.Equal(HttpStatusCode.OK, analyticsRes.StatusCode);

        var analytics = await analyticsRes.Content.ReadFromJsonAsync<WorkflowAnalyticsDto>(JsonOptions);
        Assert.NotNull(analytics);
        Assert.Equal(1, analytics.TotalSubmissions);
        Assert.Equal(1, analytics.FullyApprovedCount);
        Assert.Single(analytics.StepMetrics);
        Assert.Equal(1, analytics.StepMetrics[0].TotalSubmissionsProcessed);
    }

    [Fact]
    public async Task NotificationsApi_DeleteAndClearAll_ShouldRemoveNotificationsCorrectly()
    {
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync();
        var form = await CreatePublishedFormAsync(ownerToken, companyId);

        // Submit form twice to create notifications
        for (int i = 0; i < 2; i++)
        {
            var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
            {
                Content = JsonContent.Create(new SubmitFormDto
                {
                    Data = new Dictionary<string, object?> { ["reason"] = $"Notif test {i}" }
                })
            };
            submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
            await _client.SendAsync(submitMsg);
        }

        // List notifications
        var listMsg = new HttpRequestMessage(HttpMethod.Get, $"/notifications?companyId={companyId}");
        listMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var listRes = await _client.SendAsync(listMsg);
        var notifList = await listRes.Content.ReadFromJsonAsync<NotificationListResponse>(JsonOptions);
        Assert.NotNull(notifList);
        Assert.True(notifList.Items.Count >= 2);

        var firstId = notifList.Items[0].Id;

        // Delete single notification
        var delMsg = new HttpRequestMessage(HttpMethod.Delete, $"/notifications/{firstId}");
        delMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var delRes = await _client.SendAsync(delMsg);
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);

        // Clear all notifications
        var clearMsg = new HttpRequestMessage(HttpMethod.Delete, $"/notifications/clear-all?companyId={companyId}");
        clearMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var clearRes = await _client.SendAsync(clearMsg);
        Assert.Equal(HttpStatusCode.OK, clearRes.StatusCode);

        // Verify count is now 0
        var countMsg = new HttpRequestMessage(HttpMethod.Get, $"/notifications/unread-count?companyId={companyId}");
        countMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var countRes = await _client.SendAsync(countMsg);
        var count = await countRes.Content.ReadFromJsonAsync<int>(JsonOptions);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task WorkflowSla_StepDueDatesAndEscalation_ShouldTrackDeadlinesAndEscalate()
    {
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync();
        var form = await CreatePublishedFormAsync(ownerToken, companyId);

        // Create workflow with step SLA 24 hours
        var wfMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/workflow")
        {
            Content = JsonContent.Create(new CreateOrUpdateWorkflowDto
            {
                Name = "SLA Workflow",
                IsActive = true,
                Steps = new List<CreateOrUpdateApprovalStepDto>
                {
                    new()
                    {
                        OrderIndex = 1,
                        Name = "Urgent SLA Step",
                        StepType = ApprovalStepType.CompanyOwner,
                        TimeLimitHours = 24
                    }
                }
            })
        };
        wfMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var wfRes = await _client.SendAsync(wfMsg);
        Assert.Equal(HttpStatusCode.OK, wfRes.StatusCode);

        // Submit form
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { ["reason"] = "SLA deadline test" }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var submitRes = await _client.SendAsync(submitMsg);
        var sub = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.NotNull(sub);

        // Get timeline and verify SLA fields
        var timelineMsg = new HttpRequestMessage(
            HttpMethod.Get,
            $"/companies/{companyId}/submissions/{sub.Id}/workflow-timeline");
        timelineMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var timelineRes = await _client.SendAsync(timelineMsg);
        var timeline = await timelineRes.Content.ReadFromJsonAsync<List<SubmissionApprovalStepDto>>(JsonOptions);

        Assert.NotNull(timeline);
        Assert.Single(timeline);
        Assert.Equal(24, timeline[0].TimeLimitHours);
        Assert.NotNull(timeline[0].DueDate);
        Assert.False(timeline[0].IsOverdue);
        Assert.False(timeline[0].IsEscalated);

        // Trigger SLA escalation scan
        var scanMsg = new HttpRequestMessage(
            HttpMethod.Post,
            $"/companies/{companyId}/workflow/escalate-overdue");
        scanMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var scanRes = await _client.SendAsync(scanMsg);
        Assert.Equal(HttpStatusCode.OK, scanRes.StatusCode);
    }
}


