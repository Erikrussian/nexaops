using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexaOps.Application.Auth.DTOs;
using NexaOps.Application.Common.Exceptions;
using NexaOps.Application.Companies.DTOs;
using NexaOps.Application.CompanyMembers.DTOs;
using NexaOps.Application.Forms.DTOs;
using NexaOps.Application.Forms.Services;
using NexaOps.Application.Users.DTOs;
using NexaOps.Application.Workflows.DTOs;
using NexaOps.Domain.Entities;
using NexaOps.Domain.Enums;
using NexaOps.Infrastructure.Persistence;
using NexaOps.IntegrationTests.Common;
using Xunit;

namespace NexaOps.IntegrationTests;

public class SecurityRemediationSprint1Tests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SecurityRemediationSprint1Tests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region Helpers

    private async Task<(string Token, Guid CompanyId, Guid UserId)> SetupCompanyAndOwnerAsync(string prefix = "SecOwnerS1")
    {
        var tag = Guid.NewGuid().ToString("N");
        var registerRequest = new RegisterRequest
        {
            Name = $"{prefix} {tag}",
            Email = $"{prefix.ToLower()}.{tag}@nexaops.com",
            Password = "password123"
        };
        var regRes = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        Assert.True(regRes.IsSuccessStatusCode, $"Register failed with {regRes.StatusCode}");
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

    private async Task<(string Token, Guid UserId)> CreateUserAndJoinCompanyAsync(string ownerToken, Guid companyId, MemberRole role, string prefix = "MemberS1")
    {
        var tag = Guid.NewGuid().ToString("N");
        var email = $"{prefix.ToLower()}.{tag}@nexaops.com";
        var regRes = await _client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Name = $"{prefix} {tag}",
            Email = email,
            Password = "password123"
        });
        Assert.True(regRes.IsSuccessStatusCode, $"Register failed with {regRes.StatusCode}");
        var authResult = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        var memberToken = authResult!.AccessToken;
        var memberUserId = authResult.User.Id;

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
        Assert.True(inviteRes.IsSuccessStatusCode, $"Invite failed with {inviteRes.StatusCode}");
        var inviteData = await inviteRes.Content.ReadFromJsonAsync<InviteMemberResponse>(JsonOptions);

        var token = inviteData!.InviteLink.Split("token=")[1];

        var acceptReq = new AcceptInviteRequest { InviteToken = token };
        var acceptMsg = new HttpRequestMessage(HttpMethod.Post, "/company-members/accept-invite")
        {
            Content = JsonContent.Create(acceptReq)
        };
        acceptMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var acceptRes = await _client.SendAsync(acceptMsg);
        Assert.True(acceptRes.IsSuccessStatusCode, $"Accept invite failed with {acceptRes.StatusCode}");

        return (memberToken, memberUserId);
    }

    private async Task<(Guid FormId, Guid WorkflowId)> CreateFormWithWorkflowAsync(
        string ownerToken,
        Guid companyId,
        List<CreateOrUpdateApprovalStepDto> steps)
    {
        var formMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms")
        {
            Content = JsonContent.Create(new CreateFormDto
            {
                Title = "Form S1 " + Guid.NewGuid().ToString("N")[..6],
                Code = "FS1_" + Guid.NewGuid().ToString("N")[..6],
                Fields = new List<FormFieldDto>
                {
                    new() { Id = "notes", Name = "notes", Label = "Ghi chú", Type = FormFieldType.Text, Required = false }
                }
            })
        };
        formMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var formRes = await _client.SendAsync(formMsg);
        var form = await formRes.Content.ReadFromJsonAsync<FormDefinitionDto>(JsonOptions);

        var pubMsg = new HttpRequestMessage(HttpMethod.Patch, $"/companies/{companyId}/forms/{form!.Id}/status")
        {
            Content = JsonContent.Create(new ChangeFormStatusDto { Status = FormStatus.Published })
        };
        pubMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        await _client.SendAsync(pubMsg);

        var wfMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/workflow")
        {
            Content = JsonContent.Create(new CreateOrUpdateWorkflowDto
            {
                Name = "Workflow S1",
                IsActive = true,
                Steps = steps
            })
        };
        wfMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var wfRes = await _client.SendAsync(wfMsg);
        var wf = await wfRes.Content.ReadFromJsonAsync<ApprovalWorkflowDto>(JsonOptions);

        return (form.Id, wf!.Id);
    }

    #endregion

    #region NX-BIZ-001: Self-Approval & Terminal State Protection

    [Fact]
    public async Task NX_BIZ_001_CannotDelegateStep_WhenSubmissionInTerminalState()
    {
        // Arrange
        var (ownerToken, companyId, ownerId) = await SetupCompanyAndOwnerAsync("Biz001Owner");
        var (managerToken, managerId) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "Biz001Mgr");
        var (otherToken, otherId) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "Biz001Other");

        var steps = new List<CreateOrUpdateApprovalStepDto>
        {
            new()
            {
                OrderIndex = 1,
                Name = "Quản lý duyệt",
                StepType = ApprovalStepType.Role,
                ApproverRole = MemberRole.Manager,
                AllowSelfApproval = false
            }
        };

        var (formId, _) = await CreateFormWithWorkflowAsync(ownerToken, companyId, steps);

        // Submitter submits form
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{formId}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto { Data = new Dictionary<string, object?> { ["notes"] = "Test terminal delegation" } })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var submitRes = await _client.SendAsync(submitMsg);
        var subDto = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        // Manager reviews and approves the step via /process -> marks submission Approved (terminal state)
        var reviewMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{subDto!.Id}/steps/1/process")
        {
            Content = JsonContent.Create(new StepApprovalActionDto { Status = ApprovalStepStatus.Approved, ReviewNotes = "Duyệt hoàn tất" })
        };
        reviewMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var reviewRes = await _client.SendAsync(reviewMsg);
        Assert.Equal(HttpStatusCode.OK, reviewRes.StatusCode);

        // Act: Attempt to delegate step on terminal submission
        var delegateMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{subDto.Id}/steps/1/delegate")
        {
            Content = JsonContent.Create(new DelegateStepDto { NewApproverUserId = otherId, Reason = "Ủy quyền trễ" })
        };
        delegateMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var delegateRes = await _client.SendAsync(delegateMsg);

        // Assert: 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, delegateRes.StatusCode);
        var problem = await delegateRes.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("trạng thái kết thúc", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NX_BIZ_001_SubmitterCannotApprove_WhenSelfApprovalDisallowed()
    {
        // Arrange
        var (ownerToken, companyId, ownerId) = await SetupCompanyAndOwnerAsync("Biz001Self");
        var (managerToken, managerId) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "Biz001SelfMgr");

        var steps = new List<CreateOrUpdateApprovalStepDto>
        {
            new()
            {
                OrderIndex = 1,
                Name = "Manager Step",
                StepType = ApprovalStepType.Role,
                ApproverRole = MemberRole.Manager,
                AllowSelfApproval = false
            }
        };

        var (formId, _) = await CreateFormWithWorkflowAsync(ownerToken, companyId, steps);

        // Manager submits the form themselves
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{formId}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto { Data = new Dictionary<string, object?> { ["notes"] = "Self submission" } })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var submitRes = await _client.SendAsync(submitMsg);
        var subDto = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        // Act: Manager attempts to self-approve step 1 via /process
        var reviewMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{subDto!.Id}/steps/1/process")
        {
            Content = JsonContent.Create(new StepApprovalActionDto { Status = ApprovalStepStatus.Approved, ReviewNotes = "Self approval attempt" })
        };
        reviewMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var reviewRes = await _client.SendAsync(reviewMsg);

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, reviewRes.StatusCode);
        var problem = await reviewRes.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("tự phê duyệt", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region NX-SEC-003: ReDoS & Regex Security

    [Fact]
    public void NX_SEC_003_ReDoS_SafeTimeout_And_SafePatternEvaluation()
    {
        // Arrange: Resolve FormValidatorService
        using var scope = _factory.Services.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<IFormValidatorService>();

        // 1. Evil pattern with catastrophic backtracking: ^(a+)+$
        var evilSchema = new List<FormFieldDto>
        {
            new()
            {
                Id = "evilField",
                Name = "evilField",
                Label = "Evil Regex Field",
                Type = FormFieldType.Text,
                Required = true,
                Pattern = @"^(a+)+$"
            }
        };

        // Evil input: 45 'a' characters followed by '!'
        var evilInput = new string('a', 45) + "!";
        var data = new Dictionary<string, object?> { ["evilField"] = evilInput };

        // Act: Measure execution time
        var sw = Stopwatch.StartNew();
        var ex = Assert.Throws<ValidationException>(() => validator.ValidateSubmissionData(evilSchema, data));
        sw.Stop();

        // Assert: Must finish well under 1000ms (typical NonBacktracking or timeout is < 250ms)
        Assert.True(sw.ElapsedMilliseconds < 1000, $"ReDoS took too long: {sw.ElapsedMilliseconds}ms");
        Assert.True(ex.Errors.ContainsKey("evilField"));

        // 2. Syntactically invalid regex must not crash the service with unhandled exception
        var invalidSyntaxSchema = new List<FormFieldDto>
        {
            new()
            {
                Id = "syntaxErrorField",
                Name = "syntaxErrorField",
                Label = "Invalid Pattern",
                Type = FormFieldType.Text,
                Required = true,
                Pattern = @"[a-z(" // Malformed unclosed bracket
            }
        };
        var validData = new Dictionary<string, object?> { ["syntaxErrorField"] = "test" };
        var ex2 = Assert.Throws<ValidationException>(() => validator.ValidateSubmissionData(invalidSyntaxSchema, validData));
        Assert.True(ex2.Errors.ContainsKey("syntaxErrorField"));

        // 3. Valid regex matches correctly without throwing
        var validSchema = new List<FormFieldDto>
        {
            new()
            {
                Id = "codeField",
                Name = "codeField",
                Label = "Zip Code",
                Type = FormFieldType.Text,
                Required = true,
                Pattern = @"^\d{3}-\d{3}$"
            }
        };
        var matchingData = new Dictionary<string, object?> { ["codeField"] = "123-456" };
        validator.ValidateSubmissionData(validSchema, matchingData); // No exception thrown
    }

    [Fact]
    public void NX_SEC_003_RegexOversizedInputs_AreSafelyRejected()
    {
        using var scope = _factory.Services.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<IFormValidatorService>();

        // Pattern exceeds 500 chars
        var longPatternSchema = new List<FormFieldDto>
        {
            new()
            {
                Id = "longPattern",
                Name = "longPattern",
                Label = "Long Pattern",
                Type = FormFieldType.Text,
                Pattern = new string('a', 501)
            }
        };
        var ex1 = Assert.Throws<ValidationException>(() => validator.ValidateSubmissionData(longPatternSchema, new() { ["longPattern"] = "val" }));
        Assert.True(ex1.Errors.ContainsKey("longPattern"));
        Assert.Contains("too long", ex1.Errors["longPattern"][0], StringComparison.OrdinalIgnoreCase);

        // Input exceeds 10,000 chars
        var normalSchema = new List<FormFieldDto>
        {
            new()
            {
                Id = "longInput",
                Name = "longInput",
                Label = "Long Input",
                Type = FormFieldType.Text,
                Pattern = @"^[a-z]+$"
            }
        };
        var ex2 = Assert.Throws<ValidationException>(() => validator.ValidateSubmissionData(normalSchema, new() { ["longInput"] = new string('a', 10001) }));
        Assert.True(ex2.Errors.ContainsKey("longInput"));
        Assert.Contains("exceeds maximum allowed length", ex2.Errors["longInput"][0], StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region NX-BIZ-003: Member Directory Read Access & Sensitivity Masking

    [Fact]
    public async Task NX_BIZ_003_MemberCanReadDirectory_NonMemberForbidden_And_InviteMetadataMasked()
    {
        // Arrange
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync("Biz003Owner");
        var (memberToken, memberId) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member, "Biz003Mbr");
        var (otherOwnerToken, otherCompanyId, _) = await SetupCompanyAndOwnerAsync("Biz003External");

        // Owner sends a pending invitation
        var inviteEmail = $"pending.{Guid.NewGuid():N}@nexaops.com";
        var inviteMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/members/invite")
        {
            Content = JsonContent.Create(new InviteMemberRequest { Email = inviteEmail, Role = MemberRole.Member })
        };
        inviteMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var inviteRes = await _client.SendAsync(inviteMsg);
        Assert.True(inviteRes.IsSuccessStatusCode, $"Invite failed: {inviteRes.StatusCode}");

        // 1. Owner can list all members, including pending invites
        var ownerListMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/members");
        ownerListMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var ownerListRes = await _client.SendAsync(ownerListMsg);
        Assert.Equal(HttpStatusCode.OK, ownerListRes.StatusCode);
        var ownerList = await ownerListRes.Content.ReadFromJsonAsync<List<CompanyMemberResponse>>(JsonOptions);
        Assert.NotNull(ownerList);
        Assert.Contains(ownerList, m => m.Email.Equals(inviteEmail, StringComparison.OrdinalIgnoreCase));
        var pendingInOwnerList = ownerList.First(m => m.Email.Equals(inviteEmail, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(pendingInOwnerList.InviteExpiresAt);
        Assert.NotNull(pendingInOwnerList.InvitedById);

        // 2. Member can list members without 403 Forbidden, but sees only Active members and masked invite metadata
        var memberListMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/members");
        memberListMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var memberListRes = await _client.SendAsync(memberListMsg);
        Assert.Equal(HttpStatusCode.OK, memberListRes.StatusCode);
        var memberList = await memberListRes.Content.ReadFromJsonAsync<List<CompanyMemberResponse>>(JsonOptions);
        Assert.NotNull(memberList);

        // Pending invite MUST NOT be visible to regular member
        Assert.DoesNotContain(memberList, m => m.Email.Equals(inviteEmail, StringComparison.OrdinalIgnoreCase));
        // All returned members must have status ACTIVE
        Assert.All(memberList, m => Assert.Equal("ACTIVE", m.Status));
        // Sensitive invite metadata must be masked (null)
        Assert.All(memberList, m =>
        {
            Assert.Null(m.InviteExpiresAt);
            Assert.Null(m.InvitedById);
            Assert.Null(m.InvitedBy);
        });

        // 3. User from other company is forbidden (403)
        var externalListMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/members");
        externalListMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", otherOwnerToken);
        var externalListRes = await _client.SendAsync(externalListMsg);
        Assert.Equal(HttpStatusCode.Forbidden, externalListRes.StatusCode);

        // 4. Member attempting to invite someone is strictly forbidden (403)
        var escalateInviteMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/members/invite")
        {
            Content = JsonContent.Create(new InviteMemberRequest { Email = "fail@test.com", Role = MemberRole.Member })
        };
        escalateInviteMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var escalateRes = await _client.SendAsync(escalateInviteMsg);
        Assert.Equal(HttpStatusCode.Forbidden, escalateRes.StatusCode);
    }

    #endregion

    #region NX-BIZ-002: SLA Deadline Activation Logic

    [Fact]
    public async Task NX_BIZ_002_SlaDueDate_ActivatesOnlyWhenStepBecomesCurrent()
    {
        // Arrange: 2-step workflow with SLA time limits
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync("Biz002Sla");
        var (mgr1Token, mgr1Id) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "Biz002Mgr1");
        var (mgr2Token, mgr2Id) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "Biz002Mgr2");

        var steps = new List<CreateOrUpdateApprovalStepDto>
        {
            new()
            {
                OrderIndex = 1,
                Name = "Step 1 - Manager 1",
                StepType = ApprovalStepType.SpecificUser,
                SpecificApproverUserId = mgr1Id,
                TimeLimitHours = 24
            },
            new()
            {
                OrderIndex = 2,
                Name = "Step 2 - Manager 2",
                StepType = ApprovalStepType.SpecificUser,
                SpecificApproverUserId = mgr2Id,
                TimeLimitHours = 48
            }
        };

        var (formId, _) = await CreateFormWithWorkflowAsync(ownerToken, companyId, steps);

        // Submitter submits form
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{formId}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto { Data = new Dictionary<string, object?> { ["notes"] = "SLA test" } })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var submitRes = await _client.SendAsync(submitMsg);
        var subDto = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.NotNull(subDto);

        // Assert Step 1 DueDate is set, but Step 2 DueDate is NULL
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbSteps = await db.SubmissionApprovalSteps
                .Where(s => s.SubmissionId == subDto.Id)
                .OrderBy(s => s.OrderIndex)
                .ToListAsync();

            Assert.Equal(2, dbSteps.Count);
            // Step 1: Active, DueDate populated
            Assert.NotNull(dbSteps[0].DueDate);
            Assert.True(dbSteps[0].DueDate > DateTime.UtcNow.AddHours(20));

            // Step 2: Inactive, DueDate MUST BE NULL
            Assert.Null(dbSteps[1].DueDate);
        }

        // Escalation run should NOT escalate Step 2
        var escMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/workflow/escalate-overdue");
        escMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var escRes = await _client.SendAsync(escMsg);
        Assert.Equal(HttpStatusCode.OK, escRes.StatusCode);

        // Approve Step 1 via /process -> advances to Step 2
        var revMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{subDto.Id}/steps/1/process")
        {
            Content = JsonContent.Create(new StepApprovalActionDto { Status = ApprovalStepStatus.Approved, ReviewNotes = "Step 1 OK" })
        };
        revMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mgr1Token);
        var revRes = await _client.SendAsync(revMsg);
        Assert.Equal(HttpStatusCode.OK, revRes.StatusCode);

        // Assert Step 2 DueDate is NOW activated upon step advancing
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var step2 = await db.SubmissionApprovalSteps
                .FirstOrDefaultAsync(s => s.SubmissionId == subDto.Id && s.OrderIndex == 2);

            Assert.NotNull(step2);
            Assert.NotNull(step2.DueDate);
            Assert.True(step2.DueDate > DateTime.UtcNow.AddHours(40));
        }
    }

    #endregion

    #region NX-SEC-005: Session Revocation After Password Change

    [Fact]
    public async Task NX_SEC_005_PasswordChange_RevokesActiveRefreshTokens()
    {
        // Arrange: User registers and logs in to create multiple sessions
        var tag = Guid.NewGuid().ToString("N");
        var email = $"pwdchange.{tag}@nexaops.com";
        var password = "oldPassword123";

        var regRes = await _client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Name = $"User {tag}",
            Email = email,
            Password = password
        });
        Assert.True(regRes.IsSuccessStatusCode);
        var auth1 = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        var userId = auth1!.User.Id;
        var token1 = auth1.AccessToken;
        var refresh1 = auth1.RefreshToken;

        // Login second time (Session 2)
        var loginRes = await _client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        Assert.True(loginRes.IsSuccessStatusCode);
        var auth2 = await loginRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        var refresh2 = auth2!.RefreshToken;

        // Verify both refresh tokens exist and are active in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var activeSessions = await db.RefreshTokens
                .Where(r => r.UserId == userId && r.RevokedAt == null)
                .ToListAsync();

            Assert.True(activeSessions.Count >= 2);
        }

        // Act: User changes password via PATCH /users/{id}
        var updateMsg = new HttpRequestMessage(HttpMethod.Patch, $"/users/{userId}")
        {
            Content = JsonContent.Create(new UpdateUserRequest
            {
                Password = "newSecurePassword456!"
            })
        };
        updateMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token1);
        var updateRes = await _client.SendAsync(updateMsg);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        // Assert 1: All refresh tokens for user in DB are revoked
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var unrevokedSessions = await db.RefreshTokens
                .Where(r => r.UserId == userId && r.RevokedAt == null)
                .ToListAsync();

            Assert.Empty(unrevokedSessions);

            var revokedSessions = await db.RefreshTokens
                .Where(r => r.UserId == userId && r.RevokedAt != null)
                .ToListAsync();

            Assert.All(revokedSessions, s => Assert.Equal("Revoked due to password change", s.ReasonRevoked));
        }

        // Assert 2: Refreshing with old refresh token fails with 401 Unauthorized
        var refreshMsg = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh")
        {
            Content = JsonContent.Create(new RefreshTokenRequest { RefreshToken = refresh1 })
        };
        var refreshRes = await _client.SendAsync(refreshMsg);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshRes.StatusCode);

        // Assert 3: Login succeeds with new password
        var newLoginRes = await _client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Email = email,
            Password = "newSecurePassword456!"
        });
        Assert.Equal(HttpStatusCode.OK, newLoginRes.StatusCode);
    }

    #endregion

    #region NX-SEC-004: In-Memory Token Storage Contract

    [Fact]
    public async Task NX_SEC_004_SilentRefresh_SetsHttpOnlyCookie_And_ReturnsInMemoryAccessToken()
    {
        // Arrange
        var tag = Guid.NewGuid().ToString("N");
        var email = $"tokenstore.{tag}@nexaops.com";
        var regRes = await _client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Name = $"Store {tag}",
            Email = email,
            Password = "password123"
        });
        Assert.True(regRes.IsSuccessStatusCode, $"Register failed: {regRes.StatusCode}");

        // Inspect response cookies for refreshToken
        var hasSetCookie = regRes.Headers.TryGetValues("Set-Cookie", out var cookies);
        Assert.True(hasSetCookie);
        var cookieStr = string.Join("; ", cookies!);
        Assert.Contains("refreshToken=", cookieStr);
        Assert.Contains("httponly", cookieStr, StringComparison.OrdinalIgnoreCase);

        // Extract cookie value for request
        var rawCookie = cookies!.First(c => c.StartsWith("refreshToken=")).Split(';')[0];

        // Act: Refresh token endpoint using the HttpOnly cookie
        var refreshMsg = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        refreshMsg.Headers.Add("Cookie", rawCookie);
        var refreshRes = await _client.SendAsync(refreshMsg);
        Assert.Equal(HttpStatusCode.OK, refreshRes.StatusCode);

        var authRes = await refreshRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(authRes);
        Assert.False(string.IsNullOrWhiteSpace(authRes.AccessToken));
    }

    #endregion
}
