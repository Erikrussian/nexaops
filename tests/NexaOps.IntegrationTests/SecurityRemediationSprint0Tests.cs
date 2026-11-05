using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NexaOps.Api.Hubs;
using NexaOps.Application.Auth.DTOs;
using NexaOps.Application.Common.Interfaces;
using NexaOps.Application.Companies.DTOs;
using NexaOps.Application.CompanyMembers.DTOs;
using NexaOps.Application.Forms.DTOs;
using NexaOps.Application.Workflows.DTOs;
using NexaOps.Domain.Entities;
using NexaOps.Domain.Enums;
using NexaOps.Infrastructure.Persistence;
using NexaOps.IntegrationTests.Common;

namespace NexaOps.IntegrationTests;

public class SecurityRemediationSprint0Tests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SecurityRemediationSprint0Tests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region Helper Test Doubles for SignalR Hub Testing

    private class TestHubCallerContext : HubCallerContext
    {
        public TestHubCallerContext(string connectionId, ClaimsPrincipal? user)
        {
            ConnectionId = connectionId;
            User = user;
            UserIdentifier = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? user?.FindFirst("sub")?.Value;
        }

        public override string ConnectionId { get; }
        public override string? UserIdentifier { get; }
        public override ClaimsPrincipal? User { get; }
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features => new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }

    private class TestGroupManager : IGroupManager
    {
        public List<(string ConnectionId, string GroupName)> AddedGroups { get; } = new();
        public List<(string ConnectionId, string GroupName)> RemovedGroups { get; } = new();

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            AddedGroups.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            RemovedGroups.Add((connectionId, groupName));
            return Task.CompletedTask;
        }
    }

    private ClaimsPrincipal CreateClaimsPrincipal(Guid userId, string email, string role = "member")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("sub", userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    #endregion

    #region API Setup Helpers

    private async Task<(string Token, Guid CompanyId, Guid UserId)> SetupCompanyAndOwnerAsync(string prefix = "SecOwner")
    {
        var tag = Guid.NewGuid().ToString("N");
        var registerRequest = new RegisterRequest
        {
            Name = $"{prefix} {tag}",
            Email = $"{prefix.ToLower()}.{tag}@nexaops.com",
            Password = "password123"
        };
        var regRes = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        var authResult = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        var token = authResult!.AccessToken;

        var compRequest = new CreateCompanyRequest { Name = $"Company {tag}" };
        var msgComp = new HttpRequestMessage(HttpMethod.Post, "/companies")
        {
            Content = JsonContent.Create(compRequest)
        };
        msgComp.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var compRes = await _client.SendAsync(msgComp);
        var company = await compRes.Content.ReadFromJsonAsync<CompanyResponse>(JsonOptions);

        return (token, company!.Id, authResult.User.Id);
    }

    private async Task<(string Token, Guid UserId)> CreateUserAndJoinCompanyAsync(string ownerToken, Guid companyId, MemberRole role, string prefix = "Member")
    {
        var tag = Guid.NewGuid().ToString("N");
        var email = $"{prefix.ToLower()}.{tag}@nexaops.com";
        var regRes = await _client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Name = $"{prefix} {tag}",
            Email = email,
            Password = "password123"
        });
        var authResult = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        var memberToken = authResult!.AccessToken;
        var memberUserId = authResult.User.Id;

        // Owner invites member
        var inviteMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/members/invite")
        {
            Content = JsonContent.Create(new InviteMemberRequest
            {
                Email = email,
                Role = role
            })
        };
        inviteMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var inviteRes = await _client.SendAsync(inviteMsg);
        var inviteData = await inviteRes.Content.ReadFromJsonAsync<InviteMemberResponse>(JsonOptions);

        var token = inviteData!.InviteLink.Split("token=")[1];

        // Member accepts invite
        var acceptReq = new AcceptInviteRequest { InviteToken = token };
        var acceptMsg = new HttpRequestMessage(HttpMethod.Post, "/company-members/accept-invite")
        {
            Content = JsonContent.Create(acceptReq)
        };
        acceptMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        await _client.SendAsync(acceptMsg);

        return (memberToken, memberUserId);
    }

    private async Task<FormDefinitionDto> CreateAndPublishFormAsync(string ownerToken, Guid companyId, string title = "Expense Report")
    {
        var createMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms")
        {
            Content = JsonContent.Create(new CreateFormDto
            {
                Title = title,
                Code = "FORM_" + Guid.NewGuid().ToString("N")[..6],
                Fields = new List<FormFieldDto>
                {
                    new() { Id = "amount", Name = "amount", Label = "Số tiền", Type = FormFieldType.Number, Required = true },
                    new() { Id = "reason", Name = "reason", Label = "Lý do", Type = FormFieldType.Text, Required = true }
                }
            })
        };
        createMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var createRes = await _client.SendAsync(createMsg);
        var form = await createRes.Content.ReadFromJsonAsync<FormDefinitionDto>(JsonOptions);

        var pubMsg = new HttpRequestMessage(HttpMethod.Patch, $"/companies/{companyId}/forms/{form!.Id}/status")
        {
            Content = JsonContent.Create(new ChangeFormStatusDto { Status = FormStatus.Published })
        };
        pubMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        await _client.SendAsync(pubMsg);

        return form;
    }

    #endregion

    // =========================================================================
    // SECTION 1: NX-SEC-001 (SignalR Cross-Tenant Group Authorization Tests)
    // =========================================================================

    [Fact]
    public async Task SignalR_ActiveOwner_CanJoinOwnCompanyGroup()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var (token, companyId, ownerId) = await SetupCompanyAndOwnerAsync("SignalROwner");

        var groupManager = new TestGroupManager();
        var context = new TestHubCallerContext("conn_1", CreateClaimsPrincipal(ownerId, "owner@nexaops.com"));
        var hub = new NotificationHub(db, NullLogger<NotificationHub>.Instance)
        {
            Context = context,
            Groups = groupManager
        };

        // Act
        await hub.JoinCompany(companyId.ToString());

        // Assert
        Assert.Single(groupManager.AddedGroups);
        Assert.Equal(("conn_1", $"company_{companyId}"), groupManager.AddedGroups[0]);
    }

    [Fact]
    public async Task SignalR_ActiveMember_CanJoinAuthorizedCompanyGroup()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync("SignalROwner2");
        var (_, memberId) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member, "ActiveMember");

        var groupManager = new TestGroupManager();
        var context = new TestHubCallerContext("conn_2", CreateClaimsPrincipal(memberId, "member@nexaops.com"));
        var hub = new NotificationHub(db, NullLogger<NotificationHub>.Instance)
        {
            Context = context,
            Groups = groupManager
        };

        // Act
        await hub.JoinCompany(companyId.ToString());

        // Assert
        Assert.Single(groupManager.AddedGroups);
        Assert.Equal(("conn_2", $"company_{companyId}"), groupManager.AddedGroups[0]);
    }

    [Fact]
    public async Task SignalR_CrossTenantMember_CannotJoinOtherCompanyGroup()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var (ownerAToken, companyAId, _) = await SetupCompanyAndOwnerAsync("TenantA");
        var (_, companyBId, _) = await SetupCompanyAndOwnerAsync("TenantB");

        // Attacker is member of Company A only
        var (_, attackerUserId) = await CreateUserAndJoinCompanyAsync(ownerAToken, companyAId, MemberRole.Member, "AttackerUser");

        var groupManager = new TestGroupManager();
        var context = new TestHubCallerContext("conn_attacker", CreateClaimsPrincipal(attackerUserId, "attacker@nexaops.com"));
        var hub = new NotificationHub(db, NullLogger<NotificationHub>.Instance)
        {
            Context = context,
            Groups = groupManager
        };

        // Act & Assert: Attacker attempts to join Company B's SignalR broadcast channel
        var ex = await Assert.ThrowsAsync<HubException>(() => hub.JoinCompany(companyBId.ToString()));
        Assert.Contains("Forbidden", ex.Message);
        Assert.Empty(groupManager.AddedGroups); // Group manager must NOT have added attacker
    }

    [Fact]
    public async Task SignalR_UserWithoutAnyCompanyMembership_IsRejected()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var (_, companyId, _) = await SetupCompanyAndOwnerAsync("ValidCo");

        // Rogue user registered in system but not belonging to company
        var rogueUserId = Guid.NewGuid();

        var groupManager = new TestGroupManager();
        var context = new TestHubCallerContext("conn_rogue", CreateClaimsPrincipal(rogueUserId, "rogue@nexaops.com"));
        var hub = new NotificationHub(db, NullLogger<NotificationHub>.Instance)
        {
            Context = context,
            Groups = groupManager
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<HubException>(() => hub.JoinCompany(companyId.ToString()));
        Assert.Contains("Forbidden", ex.Message);
        Assert.Empty(groupManager.AddedGroups);
    }

    [Fact]
    public async Task SignalR_SuspendedMember_CannotJoinCompanyGroup()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync("CoSuspendedTest");
        var (_, memberId) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member, "SuspendedMember");

        // Manually suspend membership in database
        var memberEntity = await db.CompanyMembers.FirstOrDefaultAsync(m => m.CompanyId == companyId && m.UserId == memberId);
        Assert.NotNull(memberEntity);
        memberEntity.Status = MemberStatus.Suspended;
        await db.SaveChangesAsync();

        var groupManager = new TestGroupManager();
        var context = new TestHubCallerContext("conn_suspended", CreateClaimsPrincipal(memberId, "suspended@nexaops.com"));
        var hub = new NotificationHub(db, NullLogger<NotificationHub>.Instance)
        {
            Context = context,
            Groups = groupManager
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<HubException>(() => hub.JoinCompany(companyId.ToString()));
        Assert.Contains("Forbidden", ex.Message);
        Assert.Empty(groupManager.AddedGroups);
    }

    [Fact]
    public async Task SignalR_UnauthenticatedClient_ThrowsUnauthorized()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var (_, companyId, _) = await SetupCompanyAndOwnerAsync("AnonCo");

        var groupManager = new TestGroupManager();
        var context = new TestHubCallerContext("conn_anon", null); // Unauthenticated: no claims principal
        var hub = new NotificationHub(db, NullLogger<NotificationHub>.Instance)
        {
            Context = context,
            Groups = groupManager
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<HubException>(() => hub.JoinCompany(companyId.ToString()));
        Assert.Contains("Unauthorized", ex.Message);
        Assert.Empty(groupManager.AddedGroups);
    }

    [Fact]
    public async Task SignalR_InvalidCompanyId_ThrowsInvalidCompanyIdentifier()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var groupManager = new TestGroupManager();
        var context = new TestHubCallerContext("conn_invalid", CreateClaimsPrincipal(Guid.NewGuid(), "u@nexaops.com"));
        var hub = new NotificationHub(db, NullLogger<NotificationHub>.Instance)
        {
            Context = context,
            Groups = groupManager
        };

        // Act & Assert
        var ex1 = await Assert.ThrowsAsync<HubException>(() => hub.JoinCompany("not-a-guid"));
        Assert.Contains("Invalid company identifier", ex1.Message);

        var ex2 = await Assert.ThrowsAsync<HubException>(() => hub.JoinCompany(Guid.Empty.ToString()));
        Assert.Contains("Invalid company identifier", ex2.Message);

        Assert.Empty(groupManager.AddedGroups);
    }

    [Fact]
    public async Task SignalR_LeaveCompany_RemovesFromGroupSuccessfully()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var targetCompanyId = Guid.NewGuid();

        var groupManager = new TestGroupManager();
        var context = new TestHubCallerContext("conn_leave", CreateClaimsPrincipal(Guid.NewGuid(), "u@nexaops.com"));
        var hub = new NotificationHub(db, NullLogger<NotificationHub>.Instance)
        {
            Context = context,
            Groups = groupManager
        };

        // Act
        await hub.LeaveCompany(targetCompanyId.ToString());

        // Assert
        Assert.Single(groupManager.RemovedGroups);
        Assert.Equal(("conn_leave", $"company_{targetCompanyId}"), groupManager.RemovedGroups[0]);
    }

    // =========================================================================
    // SECTION 2: NX-SEC-002 (Legacy Review Workflow Bypass Protection Tests)
    // =========================================================================

    [Fact]
    public async Task WorkflowManagedSubmission_LegacyReviewApprovalAttempt_IsRejectedWithBadRequest()
    {
        // Arrange
        var (ownerToken, companyId, ownerUserId) = await SetupCompanyAndOwnerAsync("WfBypassTest");
        var (managerToken, managerUserId) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "WfManager");
        var (memberToken, memberUserId) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member, "WfMember");

        // 1. Create and Publish Form
        var form = await CreateAndPublishFormAsync(ownerToken, companyId, "High-Value Purchase Request");

        // 2. Create Multi-Step Approval Workflow:
        //    Step 1: Manager approval
        //    Step 2: Owner approval (High-value signoff)
        var createWfMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/workflow")
        {
            Content = JsonContent.Create(new CreateOrUpdateWorkflowDto
            {
                Name = "Purchase Workflow 2-Step",
                IsActive = true,
                Steps = new List<CreateOrUpdateApprovalStepDto>
                {
                    new()
                    {
                        OrderIndex = 1,
                        Name = "Manager Approval",
                        StepType = ApprovalStepType.Role,
                        ApproverRole = MemberRole.Manager,
                        AllowSelfApproval = false
                    },
                    new()
                    {
                        OrderIndex = 2,
                        Name = "Executive Signoff",
                        StepType = ApprovalStepType.CompanyOwner,
                        AllowSelfApproval = false
                    }
                }
            })
        };
        createWfMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var createWfRes = await _client.SendAsync(createWfMsg);
        Assert.Equal(HttpStatusCode.OK, createWfRes.StatusCode);

        // 3. Member submits form
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?>
                {
                    { "amount", 50000 },
                    { "reason", "Datacenter server expansion" }
                }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var submitRes = await _client.SendAsync(submitMsg);
        Assert.Equal(HttpStatusCode.Created, submitRes.StatusCode);
        var submission = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.NotNull(submission);
        Assert.Equal(SubmissionStatus.InReview, submission.Status);

        // 4. ACT: Manager attempts to bulk-approve entire submission via legacy review endpoint!
        var legacyReviewMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{submission.Id}/review")
        {
            Content = JsonContent.Create(new ReviewSubmissionDto
            {
                Status = SubmissionStatus.Approved,
                Notes = "Bypassing Owner step via legacy review!"
            })
        };
        legacyReviewMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var legacyReviewRes = await _client.SendAsync(legacyReviewMsg);

        // 5. ASSERT: Endpoint MUST reject the bypass attempt with 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, legacyReviewRes.StatusCode);

        // 6. VERIFY: Submission state was NOT mutated; Step 1 and Step 2 remain intact
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var subInDb = await db.FormSubmissions
            .Include(s => s.ApprovalSteps)
            .FirstOrDefaultAsync(s => s.Id == submission.Id);

        Assert.NotNull(subInDb);
        Assert.Equal(SubmissionStatus.InReview, subInDb.Status); // Must remain InReview!
        Assert.Equal(2, subInDb.ApprovalSteps.Count);
        Assert.All(subInDb.ApprovalSteps, step => Assert.Equal(ApprovalStepStatus.Pending, step.Status));
    }

    [Fact]
    public async Task WorkflowManagedSubmission_LegacyReviewRejectionAttempt_IsRejectedWithBadRequest()
    {
        // Arrange
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync("WfBypassReject");
        var (managerToken, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "WfMgrReject");
        var (memberToken, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member, "WfMemReject");

        var form = await CreateAndPublishFormAsync(ownerToken, companyId, "Leave Request");

        // Multi-step workflow
        var createWfMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/workflow")
        {
            Content = JsonContent.Create(new CreateOrUpdateWorkflowDto
            {
                Name = "Leave Request Workflow",
                IsActive = true,
                Steps = new List<CreateOrUpdateApprovalStepDto>
                {
                    new()
                    {
                        OrderIndex = 1,
                        Name = "Department Approval",
                        StepType = ApprovalStepType.Role,
                        ApproverRole = MemberRole.Manager,
                        AllowSelfApproval = false
                    }
                }
            })
        };
        createWfMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        await _client.SendAsync(createWfMsg);

        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { { "amount", 1 }, { "reason", "Vacation" } }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var submitRes = await _client.SendAsync(submitMsg);
        var submission = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        // Act: Manager attempts legacy review rejection
        var legacyReviewMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{submission!.Id}/review")
        {
            Content = JsonContent.Create(new ReviewSubmissionDto
            {
                Status = SubmissionStatus.Rejected,
                Notes = "Direct rejection bypass attempt"
            })
        };
        legacyReviewMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var legacyReviewRes = await _client.SendAsync(legacyReviewMsg);

        // Assert: Rejected because it bypasses workflow engine state transition rules
        Assert.Equal(HttpStatusCode.BadRequest, legacyReviewRes.StatusCode);
    }

    [Fact]
    public async Task LegacyFormWithoutWorkflow_ReviewByManager_IsPreservedAndSucceeds()
    {
        // Arrange: Simple form without an approval workflow
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync("SimpleFormCo");
        var (managerToken, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "SimpleMgr");
        var (memberToken, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member, "SimpleMem");

        var form = await CreateAndPublishFormAsync(ownerToken, companyId, "General Feedback");

        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { { "amount", 0 }, { "reason", "Great workspace" } }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var submitRes = await _client.SendAsync(submitMsg);
        var submission = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.Equal(SubmissionStatus.Pending, submission!.Status);

        // Act: Authorized manager reviews simple form
        var reviewMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{submission.Id}/review")
        {
            Content = JsonContent.Create(new ReviewSubmissionDto
            {
                Status = SubmissionStatus.Approved,
                Notes = "Legitimate legacy single-step review approved"
            })
        };
        reviewMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var reviewRes = await _client.SendAsync(reviewMsg);

        // Assert: Succeeds with 200 OK, legacy behavior fully preserved
        Assert.Equal(HttpStatusCode.OK, reviewRes.StatusCode);
        var reviewed = await reviewRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.Equal(SubmissionStatus.Approved, reviewed!.Status);
        Assert.Equal("Legitimate legacy single-step review approved", reviewed.ReviewNotes);
    }

    [Fact]
    public async Task LegacyForm_SubmitterSelfApproval_IsRejectedWithForbidden()
    {
        // Arrange: Manager submits a form without workflow
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync("SelfApproveCo");
        var (managerToken, managerUserId) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "SelfApproveMgr");

        var form = await CreateAndPublishFormAsync(ownerToken, companyId, "Personal Expense");

        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { { "amount", 100 }, { "reason", "Personal lunch" } }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var submitRes = await _client.SendAsync(submitMsg);
        var submission = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        // Act: Submitting manager attempts to review their own submission
        var reviewMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{submission!.Id}/review")
        {
            Content = JsonContent.Create(new ReviewSubmissionDto
            {
                Status = SubmissionStatus.Approved,
                Notes = "Approving my own expenses"
            })
        };
        reviewMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var reviewRes = await _client.SendAsync(reviewMsg);

        // Assert: Self-approval in legacy review must be blocked with 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, reviewRes.StatusCode);
    }

    [Fact]
    public async Task LegacyForm_AlreadyApprovedSubmission_CannotBeReReviewed()
    {
        // Arrange
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync("TerminalStateCo");
        var (managerToken, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "TermMgr");
        var (memberToken, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member, "TermMem");

        var form = await CreateAndPublishFormAsync(ownerToken, companyId, "Hardware Upgrade");

        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { { "amount", 500 }, { "reason", "RAM upgrade" } }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var submitRes = await _client.SendAsync(submitMsg);
        var submission = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        // 1. Initial review: Approve
        var reviewMsg1 = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{submission!.Id}/review")
        {
            Content = JsonContent.Create(new ReviewSubmissionDto { Status = SubmissionStatus.Approved, Notes = "First Decision" })
        };
        reviewMsg1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var reviewRes1 = await _client.SendAsync(reviewMsg1);
        Assert.Equal(HttpStatusCode.OK, reviewRes1.StatusCode);

        // 2. Act: Attempt to tamper with terminal record by reviewing again
        var reviewMsg2 = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{submission.Id}/review")
        {
            Content = JsonContent.Create(new ReviewSubmissionDto { Status = SubmissionStatus.Rejected, Notes = "Tampered Decision" })
        };
        reviewMsg2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var reviewRes2 = await _client.SendAsync(reviewMsg2);

        // Assert: Terminal state modification is rejected
        Assert.Equal(HttpStatusCode.BadRequest, reviewRes2.StatusCode);
    }

    [Fact]
    public async Task WorkflowManagedSubmission_NormalSequentialFlow_CompletesAllStepsCleanly()
    {
        // Arrange
        var (ownerToken, companyId, ownerUserId) = await SetupCompanyAndOwnerAsync("NormalFlowCo");
        var (managerToken, managerUserId) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "NormalMgr");
        var (memberToken, memberUserId) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member, "NormalMem");

        var form = await CreateAndPublishFormAsync(ownerToken, companyId, "Conference Travel");

        // 2-Step Workflow: Step 1 (Manager), Step 2 (Owner)
        var createWfMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/workflow")
        {
            Content = JsonContent.Create(new CreateOrUpdateWorkflowDto
            {
                Name = "Conference Travel Workflow",
                IsActive = true,
                Steps = new List<CreateOrUpdateApprovalStepDto>
                {
                    new()
                    {
                        OrderIndex = 1,
                        Name = "Team Manager Approval",
                        StepType = ApprovalStepType.Role,
                        ApproverRole = MemberRole.Manager,
                        AllowSelfApproval = false
                    },
                    new()
                    {
                        OrderIndex = 2,
                        Name = "Company Owner Approval",
                        StepType = ApprovalStepType.CompanyOwner,
                        AllowSelfApproval = false
                    }
                }
            })
        };
        createWfMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        await _client.SendAsync(createWfMsg);

        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { { "amount", 1200 }, { "reason", ".NET Conf Travel" } }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var submitRes = await _client.SendAsync(submitMsg);
        var submission = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        // Act 1: Step 2 approver (Owner) tries to approve early before Step 1 is done -> Rejected
        var earlyStep2Msg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{submission!.Id}/steps/2/process")
        {
            Content = JsonContent.Create(new StepApprovalActionDto { Status = ApprovalStepStatus.Approved, ReviewNotes = "Too early" })
        };
        earlyStep2Msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var earlyStep2Res = await _client.SendAsync(earlyStep2Msg);
        Assert.Equal(HttpStatusCode.BadRequest, earlyStep2Res.StatusCode);

        // Act 2: Step 1 approved by Manager -> Succeeds
        var step1Msg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{submission.Id}/steps/1/process")
        {
            Content = JsonContent.Create(new StepApprovalActionDto { Status = ApprovalStepStatus.Approved, ReviewNotes = "Manager Approved" })
        };
        step1Msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var step1Res = await _client.SendAsync(step1Msg);
        Assert.Equal(HttpStatusCode.OK, step1Res.StatusCode);

        // Act 3: Step 2 approved by Owner -> Succeeds and finalizes submission
        var step2Msg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{submission.Id}/steps/2/process")
        {
            Content = JsonContent.Create(new StepApprovalActionDto { Status = ApprovalStepStatus.Approved, ReviewNotes = "Owner Final Signoff" })
        };
        step2Msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var step2Res = await _client.SendAsync(step2Msg);
        Assert.Equal(HttpStatusCode.OK, step2Res.StatusCode);

        // Assert: Submission final status is Approved
        var getSubMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/submissions/{submission.Id}");
        getSubMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var getSubRes = await _client.SendAsync(getSubMsg);
        var finalSub = await getSubRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.NotNull(finalSub);
        Assert.Equal(SubmissionStatus.Approved, finalSub.Status);
    }
}
