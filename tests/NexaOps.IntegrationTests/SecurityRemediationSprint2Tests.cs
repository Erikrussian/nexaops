using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexaOps.Application.AuditLogs.DTOs;
using NexaOps.Application.Auth.DTOs;
using NexaOps.Application.Common.Models;
using NexaOps.Application.Companies.DTOs;
using NexaOps.Application.CompanyMembers.DTOs;
using NexaOps.Application.Departments.DTOs;
using NexaOps.Application.Forms.DTOs;
using NexaOps.Application.Workflows.DTOs;
using NexaOps.Domain.Entities;
using NexaOps.Domain.Enums;
using NexaOps.Infrastructure.Persistence;
using NexaOps.IntegrationTests.Common;
using Xunit;

namespace NexaOps.IntegrationTests;

public class SecurityRemediationSprint2Tests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SecurityRemediationSprint2Tests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region Helpers

    private async Task<(string Token, Guid CompanyId)> SetupCompanyAsync(string prefix = "DeptSec")
    {
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync(prefix);
        return (token, companyId);
    }

    private async Task<(string Token, Guid CompanyId, Guid OwnerId)> SetupCompanyAndOwnerAsync(string prefix = "DeptSec")
    {
        var tag = Guid.NewGuid().ToString("N");
        var registerRequest = new RegisterRequest
        {
            Name = $"{prefix} {tag}",
            Email = $"{prefix.ToLower()}.{tag}@nexaops.com",
            Password = "password123"
        };
        var regRes = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        Assert.True(regRes.IsSuccessStatusCode);
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

    private async Task<(string Token, Guid UserId, Guid MemberId)> CreateUserAndJoinCompanyAsync(
        string ownerToken,
        Guid companyId,
        MemberRole role,
        string prefix = "MemberS2")
    {
        var tag = Guid.NewGuid().ToString("N");
        var email = $"{prefix.ToLower()}.{tag}@nexaops.com";
        var regRes = await _client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Name = $"{prefix} {tag}",
            Email = email,
            Password = "password123"
        });
        Assert.True(regRes.IsSuccessStatusCode);
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
        Assert.True(inviteRes.IsSuccessStatusCode);
        var inviteData = await inviteRes.Content.ReadFromJsonAsync<InviteMemberResponse>(JsonOptions);
        var memberId = inviteData!.Member.Id;

        var token = inviteData.InviteLink.Split("token=")[1];

        var acceptReq = new AcceptInviteRequest { InviteToken = token };
        var acceptMsg = new HttpRequestMessage(HttpMethod.Post, "/company-members/accept-invite")
        {
            Content = JsonContent.Create(acceptReq)
        };
        acceptMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var acceptRes = await _client.SendAsync(acceptMsg);
        Assert.True(acceptRes.IsSuccessStatusCode);

        return (memberToken, memberUserId, memberId);
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
                Title = "Form S2 " + Guid.NewGuid().ToString("N")[..6],
                Code = "FS2_" + Guid.NewGuid().ToString("N")[..6],
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
                Name = "Workflow S2",
                IsActive = true,
                Steps = steps
            })
        };
        wfMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var wfRes = await _client.SendAsync(wfMsg);
        var wf = await wfRes.Content.ReadFromJsonAsync<ApprovalWorkflowDto>(JsonOptions);

        return (form.Id, wf!.Id);
    }

    private async Task<DepartmentResponse> CreateDepartmentAsync(string token, Guid companyId, string name, Guid? parentId = null)
    {
        var req = new CreateDepartmentRequest
        {
            Name = name,
            Code = "D_" + Guid.NewGuid().ToString("N")[..6],
            ParentId = parentId
        };
        var msg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/departments")
        {
            Content = JsonContent.Create(req)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _client.SendAsync(msg);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var dept = await res.Content.ReadFromJsonAsync<DepartmentResponse>(JsonOptions);
        Assert.NotNull(dept);
        return dept;
    }

    #endregion

    #region NX-BIZ-004: Department Hierarchy Cycle Prevention

    [Fact]
    public async Task NX_BIZ_004_DepartmentCannotBecomeItsOwnParent()
    {
        // Arrange
        var (token, companyId) = await SetupCompanyAsync("SelfParent");
        var deptA = await CreateDepartmentAsync(token, companyId, "Phòng A");

        // Act: Update Dept A with ParentId = Dept A
        var updateReq = new UpdateDepartmentRequest { ParentId = deptA.Id };
        var msg = new HttpRequestMessage(HttpMethod.Patch, $"/departments/{deptA.Id}")
        {
            Content = JsonContent.Create(updateReq)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _client.SendAsync(msg);

        // Assert: 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("không thể làm cha của chính nó", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NX_BIZ_004_DirectCycle_A_to_B_to_A_IsRejected()
    {
        // Arrange: A -> B (B's parent is A)
        var (token, companyId) = await SetupCompanyAsync("CycleAB");
        var deptA = await CreateDepartmentAsync(token, companyId, "Phòng A");
        var deptB = await CreateDepartmentAsync(token, companyId, "Phòng B", deptA.Id);

        // Act: Try setting A's parent to B (creating A -> B -> A)
        var updateReq = new UpdateDepartmentRequest { ParentId = deptB.Id };
        var msg = new HttpRequestMessage(HttpMethod.Patch, $"/departments/{deptA.Id}")
        {
            Content = JsonContent.Create(updateReq)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _client.SendAsync(msg);

        // Assert: 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("chu trình lặp", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NX_BIZ_004_MultiLevelCycle_A_to_B_to_C_to_A_IsRejected()
    {
        // Arrange: A -> B -> C (B's parent is A, C's parent is B)
        var (token, companyId) = await SetupCompanyAsync("CycleABC");
        var deptA = await CreateDepartmentAsync(token, companyId, "Khối Kỹ Thuật");
        var deptB = await CreateDepartmentAsync(token, companyId, "Phòng Phát Triển", deptA.Id);
        var deptC = await CreateDepartmentAsync(token, companyId, "Nhóm Backend", deptB.Id);

        // Act: Try setting A's parent to C (creating A -> B -> C -> A)
        var updateReq = new UpdateDepartmentRequest { ParentId = deptC.Id };
        var msg = new HttpRequestMessage(HttpMethod.Patch, $"/departments/{deptA.Id}")
        {
            Content = JsonContent.Create(updateReq)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _client.SendAsync(msg);

        // Assert: 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("chu trình lặp", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NX_BIZ_004_ValidReparenting_Succeeds()
    {
        // Arrange: Parent1 -> Child, Parent2
        var (token, companyId) = await SetupCompanyAsync("ReparentValid");
        var parent1 = await CreateDepartmentAsync(token, companyId, "Phòng Kinh Doanh 1");
        var parent2 = await CreateDepartmentAsync(token, companyId, "Phòng Kinh Doanh 2");
        var child = await CreateDepartmentAsync(token, companyId, "Nhóm Bán Hàng", parent1.Id);

        // Act: Move child from Parent1 to Parent2
        var updateReq = new UpdateDepartmentRequest { ParentId = parent2.Id };
        var msg = new HttpRequestMessage(HttpMethod.Patch, $"/departments/{child.Id}")
        {
            Content = JsonContent.Create(updateReq)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _client.SendAsync(msg);

        // Assert: 200 OK and ParentId updated
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var updatedChild = await res.Content.ReadFromJsonAsync<DepartmentResponse>(JsonOptions);
        Assert.NotNull(updatedChild);
        Assert.Equal(parent2.Id, updatedChild.ParentId);
    }

    [Fact]
    public async Task NX_BIZ_004_MovingDepartmentToRoot_Succeeds()
    {
        // Arrange: Parent -> Child
        var (token, companyId) = await SetupCompanyAsync("MoveToRoot");
        var parent = await CreateDepartmentAsync(token, companyId, "Phòng Tổng Hợp");
        var child = await CreateDepartmentAsync(token, companyId, "Phòng Chuyên Môn", parent.Id);

        // Act: Move child to root by passing Guid.Empty
        var updateReq = new UpdateDepartmentRequest { ParentId = Guid.Empty };
        var msg = new HttpRequestMessage(HttpMethod.Patch, $"/departments/{child.Id}")
        {
            Content = JsonContent.Create(updateReq)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _client.SendAsync(msg);

        // Assert: 200 OK and ParentId is now null
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var updatedChild = await res.Content.ReadFromJsonAsync<DepartmentResponse>(JsonOptions);
        Assert.NotNull(updatedChild);
        Assert.Null(updatedChild.ParentId);
    }

    [Fact]
    public async Task NX_BIZ_004_CrossCompanyParentAssignment_IsRejected()
    {
        // Arrange: Company 1 and Company 2
        var (token1, companyId1) = await SetupCompanyAsync("CrossCo1");
        var (token2, companyId2) = await SetupCompanyAsync("CrossCo2");

        var dept1 = await CreateDepartmentAsync(token1, companyId1, "Phòng Công Ty 1");
        var dept2 = await CreateDepartmentAsync(token2, companyId2, "Phòng Công Ty 2");

        // Act: Try setting dept1's parent to dept2 (different company)
        var updateReq = new UpdateDepartmentRequest { ParentId = dept2.Id };
        var msg = new HttpRequestMessage(HttpMethod.Patch, $"/departments/{dept1.Id}")
        {
            Content = JsonContent.Create(updateReq)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token1);
        var res = await _client.SendAsync(msg);

        // Assert: 404 Not Found (parent not found in this company)
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task NX_BIZ_004_MissingParentDepartment_IsRejected()
    {
        // Arrange
        var (token, companyId) = await SetupCompanyAsync("MissingParent");
        var dept = await CreateDepartmentAsync(token, companyId, "Phòng Đơn");

        // Act: Update with non-existent ParentId
        var updateReq = new UpdateDepartmentRequest { ParentId = Guid.NewGuid() };
        var msg = new HttpRequestMessage(HttpMethod.Patch, $"/departments/{dept.Id}")
        {
            Content = JsonContent.Create(updateReq)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _client.SendAsync(msg);

        // Assert: 404 Not Found
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task NX_BIZ_004_GetDepartmentTree_ReturnsStructuredHierarchy()
    {
        // Arrange: Root -> Child -> SubChild
        var (token, companyId) = await SetupCompanyAsync("TreeStruct");
        var root = await CreateDepartmentAsync(token, companyId, "Khối Điều Hành");
        var child = await CreateDepartmentAsync(token, companyId, "Phòng Nhân Sự", root.Id);
        var subChild = await CreateDepartmentAsync(token, companyId, "Tổ Tuyển Dụng", child.Id);

        // Act: Get Tree
        var msg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/departments?tree=true");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _client.SendAsync(msg);

        // Assert: 200 OK with nested tree
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var tree = await res.Content.ReadFromJsonAsync<List<DepartmentTreeNodeResponse>>(JsonOptions);
        Assert.NotNull(tree);
        var rootNode = tree.FirstOrDefault(d => d.Id == root.Id);
        Assert.NotNull(rootNode);
        Assert.Single(rootNode.Children);
        Assert.Equal(child.Id, rootNode.Children[0].Id);
        Assert.Single(rootNode.Children[0].Children);
        Assert.Equal(subChild.Id, rootNode.Children[0].Children[0].Id);
    }

    #endregion

    #region NX-BIZ-005: Approval Delegation Membership Validation

    [Fact]
    public async Task NX_BIZ_005_DelegationToActiveEligibleMember_Succeeds()
    {
        // Arrange
        var (ownerToken, companyId, ownerId) = await SetupCompanyAndOwnerAsync("DelActive");
        var (mgr1Token, mgr1Id, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "DelActiveMgr1");
        var (mgr2Token, mgr2Id, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "DelActiveMgr2");

        var steps = new List<CreateOrUpdateApprovalStepDto>
        {
            new()
            {
                OrderIndex = 1,
                Name = "Manager Review",
                StepType = ApprovalStepType.Role,
                ApproverRole = MemberRole.Manager,
                AllowSelfApproval = false
            }
        };
        var (formId, _) = await CreateFormWithWorkflowAsync(ownerToken, companyId, steps);

        // Submitter (Owner) submits form
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{formId}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto { Data = new Dictionary<string, object?> { ["notes"] = "Test delegation" } })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var submitRes = await _client.SendAsync(submitMsg);
        var subDto = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        // Act: Manager 1 delegates step 1 to active Manager 2
        var delMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{subDto!.Id}/steps/1/delegate")
        {
            Content = JsonContent.Create(new DelegateStepDto { NewApproverUserId = mgr2Id, Reason = "Bận công tác" })
        };
        delMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mgr1Token);
        var delRes = await _client.SendAsync(delMsg);

        // Assert: 200 OK and SpecificApproverUserId updated to Manager 2
        Assert.Equal(HttpStatusCode.OK, delRes.StatusCode);
        var stepDto = await delRes.Content.ReadFromJsonAsync<SubmissionApprovalStepDto>(JsonOptions);
        Assert.NotNull(stepDto);
        Assert.Equal(mgr2Id, stepDto.SpecificApproverUserId);
        Assert.Equal(ApprovalStepType.SpecificUser, stepDto.StepType);

        // Manager 2 can now process the step
        var procMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{subDto.Id}/steps/1/process")
        {
            Content = JsonContent.Create(new StepApprovalActionDto { Status = ApprovalStepStatus.Approved, ReviewNotes = "Đồng ý" })
        };
        procMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mgr2Token);
        var procRes = await _client.SendAsync(procMsg);
        Assert.Equal(HttpStatusCode.OK, procRes.StatusCode);
    }

    [Fact]
    public async Task NX_BIZ_005_DelegationToSuspendedMember_IsRejected()
    {
        // Arrange
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync("DelSusp");
        var (mgr1Token, mgr1Id, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "DelSuspMgr1");
        var (_, mgr2Id, mgr2MemberId) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "DelSuspMgr2");

        var steps = new List<CreateOrUpdateApprovalStepDto>
        {
            new()
            {
                OrderIndex = 1,
                Name = "Manager Step",
                StepType = ApprovalStepType.Role,
                ApproverRole = MemberRole.Manager
            }
        };
        var (formId, _) = await CreateFormWithWorkflowAsync(ownerToken, companyId, steps);

        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{formId}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto { Data = new Dictionary<string, object?> { ["notes"] = "Test" } })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var submitRes = await _client.SendAsync(submitMsg);
        var subDto = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        // Owner suspends Manager 2
        var suspendMsg = new HttpRequestMessage(HttpMethod.Patch, $"/company-members/{mgr2MemberId}")
        {
            Content = JsonContent.Create(new UpdateMemberRequest { Status = MemberStatus.Suspended })
        };
        suspendMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var suspRes = await _client.SendAsync(suspendMsg);
        Assert.Equal(HttpStatusCode.OK, suspRes.StatusCode);

        // Act: Manager 1 attempts to delegate step 1 to suspended Manager 2
        var delMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{subDto!.Id}/steps/1/delegate")
        {
            Content = JsonContent.Create(new DelegateStepDto { NewApproverUserId = mgr2Id, Reason = "Ủy quyền" })
        };
        delMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mgr1Token);
        var delRes = await _client.SendAsync(delMsg);

        // Assert: 400 Bad Request with explicit suspended error message
        Assert.Equal(HttpStatusCode.BadRequest, delRes.StatusCode);
        var problem = await delRes.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("đình chỉ", problem.Detail, StringComparison.OrdinalIgnoreCase);

        // Assert DB state did not mutate
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbStep = await db.SubmissionApprovalSteps.FirstAsync(s => s.SubmissionId == subDto.Id && s.OrderIndex == 1);
        Assert.NotEqual(mgr2Id, dbStep.SpecificApproverUserId);
    }

    [Fact]
    public async Task NX_BIZ_005_DelegationToPendingMember_IsRejected()
    {
        // Arrange
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync("DelPend");
        var (mgr1Token, mgr1Id, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "DelPendMgr1");

        // Register a user that will be invited but NOT accept
        var tag = Guid.NewGuid().ToString("N");
        var pendingEmail = $"pending.{tag}@nexaops.com";
        var regRes = await _client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Name = $"Pending User {tag}",
            Email = pendingEmail,
            Password = "password123"
        });
        var authResult = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        var pendingUserId = authResult!.User.Id;

        // Owner invites this user (status becomes Invited)
        var inviteMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/members/invite")
        {
            Content = JsonContent.Create(new InviteMemberRequest { Email = pendingEmail, Role = MemberRole.Manager })
        };
        inviteMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var inviteRes = await _client.SendAsync(inviteMsg);
        Assert.True(inviteRes.IsSuccessStatusCode);

        var steps = new List<CreateOrUpdateApprovalStepDto>
        {
            new() { OrderIndex = 1, Name = "Manager Step", StepType = ApprovalStepType.Role, ApproverRole = MemberRole.Manager }
        };
        var (formId, _) = await CreateFormWithWorkflowAsync(ownerToken, companyId, steps);

        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{formId}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto { Data = new Dictionary<string, object?> { ["notes"] = "Test" } })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var submitRes = await _client.SendAsync(submitMsg);
        var subDto = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        // Act: Manager 1 attempts to delegate to pending user
        var delMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{subDto!.Id}/steps/1/delegate")
        {
            Content = JsonContent.Create(new DelegateStepDto { NewApproverUserId = pendingUserId, Reason = "Ủy quyền" })
        };
        delMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mgr1Token);
        var delRes = await _client.SendAsync(delMsg);

        // Assert: 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, delRes.StatusCode);
        var problem = await delRes.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("chưa kích hoạt", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NX_BIZ_005_DelegationToRemovedMember_IsRejected()
    {
        // Arrange
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync("DelRem");
        var (mgr1Token, mgr1Id, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "DelRemMgr1");
        var (_, mgr2Id, mgr2MemberId) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "DelRemMgr2");

        var steps = new List<CreateOrUpdateApprovalStepDto>
        {
            new() { OrderIndex = 1, Name = "Manager Step", StepType = ApprovalStepType.Role, ApproverRole = MemberRole.Manager }
        };
        var (formId, _) = await CreateFormWithWorkflowAsync(ownerToken, companyId, steps);

        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{formId}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto { Data = new Dictionary<string, object?> { ["notes"] = "Test" } })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var submitRes = await _client.SendAsync(submitMsg);
        var subDto = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        // Owner deletes Manager 2 from company
        var deleteMsg = new HttpRequestMessage(HttpMethod.Delete, $"/company-members/{mgr2MemberId}");
        deleteMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var deleteRes = await _client.SendAsync(deleteMsg);
        Assert.Equal(HttpStatusCode.OK, deleteRes.StatusCode);

        // Act: Manager 1 attempts to delegate to removed Manager 2
        var delMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{subDto!.Id}/steps/1/delegate")
        {
            Content = JsonContent.Create(new DelegateStepDto { NewApproverUserId = mgr2Id, Reason = "Ủy quyền" })
        };
        delMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mgr1Token);
        var delRes = await _client.SendAsync(delMsg);

        // Assert: 404 Not Found (no longer in company)
        Assert.Equal(HttpStatusCode.NotFound, delRes.StatusCode);
    }

    [Fact]
    public async Task NX_BIZ_005_CrossTenantDelegation_IsRejected()
    {
        // Arrange: Company 1 & Company 2
        var (owner1Token, companyId1, _) = await SetupCompanyAndOwnerAsync("DelCo1");
        var (owner2Token, companyId2, _) = await SetupCompanyAndOwnerAsync("DelCo2");
        var (mgr1Token, mgr1Id, _) = await CreateUserAndJoinCompanyAsync(owner1Token, companyId1, MemberRole.Manager, "DelCo1Mgr");
        var (_, mgr2Id, _) = await CreateUserAndJoinCompanyAsync(owner2Token, companyId2, MemberRole.Manager, "DelCo2Mgr");

        var steps = new List<CreateOrUpdateApprovalStepDto>
        {
            new() { OrderIndex = 1, Name = "Step 1", StepType = ApprovalStepType.Role, ApproverRole = MemberRole.Manager }
        };
        var (formId, _) = await CreateFormWithWorkflowAsync(owner1Token, companyId1, steps);

        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId1}/forms/{formId}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto { Data = new Dictionary<string, object?> { ["notes"] = "Cross-tenant" } })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", owner1Token);
        var submitRes = await _client.SendAsync(submitMsg);
        var subDto = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        // Act: Attempt to delegate to manager of Company 2
        var delMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId1}/submissions/{subDto!.Id}/steps/1/delegate")
        {
            Content = JsonContent.Create(new DelegateStepDto { NewApproverUserId = mgr2Id, Reason = "Cross-tenant delegation" })
        };
        delMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mgr1Token);
        var delRes = await _client.SendAsync(delMsg);

        // Assert: 404 Not Found
        Assert.Equal(HttpStatusCode.NotFound, delRes.StatusCode);
    }

    [Fact]
    public async Task NX_BIZ_005_UnauthorizedUser_CannotDelegate()
    {
        // Arrange
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync("DelUnauth");
        var (mgr1Token, mgr1Id, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "DelUnauthMgr1");
        var (memberToken, memberId, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member, "DelUnauthMbr");

        var steps = new List<CreateOrUpdateApprovalStepDto>
        {
            new() { OrderIndex = 1, Name = "Manager Only", StepType = ApprovalStepType.Role, ApproverRole = MemberRole.Manager }
        };
        var (formId, _) = await CreateFormWithWorkflowAsync(ownerToken, companyId, steps);

        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{formId}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto { Data = new Dictionary<string, object?> { ["notes"] = "Test" } })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var submitRes = await _client.SendAsync(submitMsg);
        var subDto = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        // Act: Regular member (not a manager/admin/owner) attempts to delegate
        var delMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{subDto!.Id}/steps/1/delegate")
        {
            Content = JsonContent.Create(new DelegateStepDto { NewApproverUserId = mgr1Id })
        };
        delMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var delRes = await _client.SendAsync(delMsg);

        // Assert: 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, delRes.StatusCode);
    }

    [Fact]
    public async Task NX_BIZ_005_DelegateBecomingInactiveAfterAssignment_CannotApprove()
    {
        // Arrange
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync("DelInact");
        var (mgr1Token, mgr1Id, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "DelInactMgr1");
        var (mgr2Token, mgr2Id, mgr2MemberId) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "DelInactMgr2");

        var steps = new List<CreateOrUpdateApprovalStepDto>
        {
            new() { OrderIndex = 1, Name = "Step 1", StepType = ApprovalStepType.Role, ApproverRole = MemberRole.Manager }
        };
        var (formId, _) = await CreateFormWithWorkflowAsync(ownerToken, companyId, steps);

        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{formId}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto { Data = new Dictionary<string, object?> { ["notes"] = "Test" } })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var submitRes = await _client.SendAsync(submitMsg);
        var subDto = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        // 1. Delegate step to active Manager 2 -> Succeeds
        var delMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{subDto!.Id}/steps/1/delegate")
        {
            Content = JsonContent.Create(new DelegateStepDto { NewApproverUserId = mgr2Id, Reason = "Chuyển giao" })
        };
        delMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mgr1Token);
        var delRes = await _client.SendAsync(delMsg);
        Assert.Equal(HttpStatusCode.OK, delRes.StatusCode);

        // 2. Manager 2 is subsequently suspended by Owner
        var suspMsg = new HttpRequestMessage(HttpMethod.Patch, $"/company-members/{mgr2MemberId}")
        {
            Content = JsonContent.Create(new UpdateMemberRequest { Status = MemberStatus.Suspended })
        };
        suspMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var suspRes = await _client.SendAsync(suspMsg);
        Assert.Equal(HttpStatusCode.OK, suspRes.StatusCode);

        // 3. Manager 2 attempts to process/approve the step -> Blocked with 403 Forbidden
        var procMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{subDto.Id}/steps/1/process")
        {
            Content = JsonContent.Create(new StepApprovalActionDto { Status = ApprovalStepStatus.Approved, ReviewNotes = "Duyệt" })
        };
        procMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mgr2Token);
        var procRes = await _client.SendAsync(procMsg);
        Assert.Equal(HttpStatusCode.Forbidden, procRes.StatusCode);

        // 4. Owner recovers workflow by re-delegating to active Manager 1 -> Succeeds
        var recoverMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{subDto.Id}/steps/1/delegate")
        {
            Content = JsonContent.Create(new DelegateStepDto { NewApproverUserId = mgr1Id, Reason = "Thu hồi và giao lại" })
        };
        recoverMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var recoverRes = await _client.SendAsync(recoverMsg);
        Assert.Equal(HttpStatusCode.OK, recoverRes.StatusCode);

        // 5. Manager 1 approves successfully
        var finalProcMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{subDto.Id}/steps/1/process")
        {
            Content = JsonContent.Create(new StepApprovalActionDto { Status = ApprovalStepStatus.Approved, ReviewNotes = "Duyệt hợp lệ" })
        };
        finalProcMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mgr1Token);
        var finalProcRes = await _client.SendAsync(finalProcMsg);
        Assert.Equal(HttpStatusCode.OK, finalProcRes.StatusCode);
    }

    #endregion

    #region NX-DATA-001: JSONB Submission Payload Validation

    private async Task<FormDefinitionDto> CreatePublishedFormWithSchemaAsync(
        string ownerToken,
        Guid companyId,
        List<FormFieldDto> fields,
        string title = "Form Schema Test")
    {
        var formMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms")
        {
            Content = JsonContent.Create(new CreateFormDto
            {
                Title = $"{title} {Guid.NewGuid():N}",
                Code = "FST_" + Guid.NewGuid().ToString("N")[..6],
                Fields = fields
            })
        };
        formMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var formRes = await _client.SendAsync(formMsg);
        Assert.Equal(HttpStatusCode.Created, formRes.StatusCode);
        var form = await formRes.Content.ReadFromJsonAsync<FormDefinitionDto>(JsonOptions);

        var pubMsg = new HttpRequestMessage(HttpMethod.Patch, $"/companies/{companyId}/forms/{form!.Id}/status")
        {
            Content = JsonContent.Create(new ChangeFormStatusDto { Status = FormStatus.Published })
        };
        pubMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var pubRes = await _client.SendAsync(pubMsg);
        Assert.Equal(HttpStatusCode.OK, pubRes.StatusCode);

        return form;
    }

    [Fact]
    public async Task NX_DATA_001_ValidPayload_ConformingToSchema_Succeeds()
    {
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync("ValidPayload");
        var fields = new List<FormFieldDto>
        {
            new() { Id = "fullName", Name = "fullName", Label = "Họ và tên", Type = FormFieldType.Text, Required = true },
            new() { Id = "age", Name = "age", Label = "Tuổi", Type = FormFieldType.Number, Required = true, Min = 18, Max = 100 },
            new() { Id = "email", Name = "email", Label = "Email", Type = FormFieldType.Email, Required = true },
            new() { Id = "department", Name = "department", Label = "Phòng ban", Type = FormFieldType.Select, Required = true, Options = new() { "Eng", "HR" } },
            new() { Id = "skills", Name = "skills", Label = "Kỹ năng", Type = FormFieldType.Checkbox, Required = false, Options = new() { "C#", "React", "SQL" } }
        };
        var form = await CreatePublishedFormWithSchemaAsync(token, companyId, fields);

        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?>
                {
                    ["fullName"] = "Nguyen Van A",
                    ["age"] = 28,
                    ["email"] = "nguyenvana@nexaops.com",
                    ["department"] = "Eng",
                    ["skills"] = new List<string> { "C#", "SQL" }
                }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var submitRes = await _client.SendAsync(submitMsg);

        Assert.Equal(HttpStatusCode.Created, submitRes.StatusCode);
        var subDto = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.NotNull(subDto);
        Assert.Equal("Nguyen Van A", subDto.Data["fullName"]?.ToString());
    }

    [Fact]
    public async Task NX_DATA_001_RequiredFields_MissingOrNull_ReturnsBadRequest()
    {
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync("ReqField");
        var fields = new List<FormFieldDto>
        {
            new() { Id = "fullName", Name = "fullName", Label = "Họ và tên", Type = FormFieldType.Text, Required = true },
            new() { Id = "email", Name = "email", Label = "Email", Type = FormFieldType.Email, Required = true }
        };
        var form = await CreatePublishedFormWithSchemaAsync(token, companyId, fields);

        // Missing required field fullName
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?>
                {
                    ["email"] = "test@nexaops.com"
                }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var submitRes = await _client.SendAsync(submitMsg);

        Assert.Equal(HttpStatusCode.BadRequest, submitRes.StatusCode);
    }

    [Fact]
    public async Task NX_DATA_001_OptionalFields_CanBeOmittedOrNull_Succeeds()
    {
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync("OptField");
        var fields = new List<FormFieldDto>
        {
            new() { Id = "title", Name = "title", Label = "Tiêu đề", Type = FormFieldType.Text, Required = true },
            new() { Id = "notes", Name = "notes", Label = "Ghi chú", Type = FormFieldType.Textarea, Required = false }
        };
        var form = await CreatePublishedFormWithSchemaAsync(token, companyId, fields);

        // Omit optional field notes
        var submitMsg1 = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { ["title"] = "Báo cáo A" }
            })
        };
        submitMsg1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var submitRes1 = await _client.SendAsync(submitMsg1);
        Assert.Equal(HttpStatusCode.Created, submitRes1.StatusCode);

        // Send null for optional field notes
        var submitMsg2 = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { ["title"] = "Báo cáo B", ["notes"] = null }
            })
        };
        submitMsg2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var submitRes2 = await _client.SendAsync(submitMsg2);
        Assert.Equal(HttpStatusCode.Created, submitRes2.StatusCode);
    }

    [Fact]
    public async Task NX_DATA_001_UnexpectedTopLevelField_ReturnsBadRequest()
    {
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync("UnexpTop");
        var fields = new List<FormFieldDto>
        {
            new() { Id = "name", Name = "name", Label = "Tên", Type = FormFieldType.Text, Required = true }
        };
        var form = await CreatePublishedFormWithSchemaAsync(token, companyId, fields);

        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?>
                {
                    ["name"] = "Alice",
                    ["isAdmin"] = true,
                    ["__injectedRole"] = "Owner"
                }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var submitRes = await _client.SendAsync(submitMsg);

        Assert.Equal(HttpStatusCode.BadRequest, submitRes.StatusCode);
        var responseBody = await submitRes.Content.ReadAsStringAsync();
        Assert.Contains("isAdmin", responseBody);
        Assert.Contains("__injectedRole", responseBody);
    }

    [Fact]
    public async Task NX_DATA_001_UnexpectedNestedField_ReturnsBadRequest()
    {
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync("UnexpNest");
        var fields = new List<FormFieldDto>
        {
            new()
            {
                Id = "address",
                Name = "address",
                Label = "Địa chỉ",
                Type = FormFieldType.Text,
                Fields = new List<FormFieldDto>
                {
                    new() { Id = "street", Name = "street", Label = "Đường", Type = FormFieldType.Text, Required = true },
                    new() { Id = "city", Name = "city", Label = "Thành phố", Type = FormFieldType.Text, Required = true }
                }
            }
        };
        var form = await CreatePublishedFormWithSchemaAsync(token, companyId, fields);

        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?>
                {
                    ["address"] = new Dictionary<string, object?>
                    {
                        ["street"] = "123 Main St",
                        ["city"] = "Hanoi",
                        ["unauthorizedSecret"] = "maliciousData"
                    }
                }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var submitRes = await _client.SendAsync(submitMsg);

        Assert.Equal(HttpStatusCode.BadRequest, submitRes.StatusCode);
        var responseBody = await submitRes.Content.ReadAsStringAsync();
        Assert.Contains("address.unauthorizedSecret", responseBody);
    }

    [Fact]
    public async Task NX_DATA_001_InvalidFieldType_NumberField_NonNumeric_ReturnsBadRequest()
    {
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync("InvNum");
        var fields = new List<FormFieldDto>
        {
            new() { Id = "amount", Name = "amount", Label = "Số tiền", Type = FormFieldType.Number, Required = true }
        };
        var form = await CreatePublishedFormWithSchemaAsync(token, companyId, fields);

        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?>
                {
                    ["amount"] = "not_a_number"
                }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var submitRes = await _client.SendAsync(submitMsg);

        Assert.Equal(HttpStatusCode.BadRequest, submitRes.StatusCode);
        var responseBody = await submitRes.Content.ReadAsStringAsync();
        Assert.Contains("amount", responseBody);
    }

    [Fact]
    public async Task NX_DATA_001_InvalidFieldType_EmailField_ReturnsBadRequest()
    {
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync("InvEmail");
        var fields = new List<FormFieldDto>
        {
            new() { Id = "contactEmail", Name = "contactEmail", Label = "Email liên hệ", Type = FormFieldType.Email, Required = true }
        };
        var form = await CreatePublishedFormWithSchemaAsync(token, companyId, fields);

        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?>
                {
                    ["contactEmail"] = "invalid-email-string"
                }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var submitRes = await _client.SendAsync(submitMsg);

        Assert.Equal(HttpStatusCode.BadRequest, submitRes.StatusCode);
    }

    [Fact]
    public async Task NX_DATA_001_InvalidSelectOption_ReturnsBadRequest()
    {
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync("InvSelect");
        var fields = new List<FormFieldDto>
        {
            new() { Id = "branch", Name = "branch", Label = "Chi nhánh", Type = FormFieldType.Select, Required = true, Options = new() { "North", "South" } }
        };
        var form = await CreatePublishedFormWithSchemaAsync(token, companyId, fields);

        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?>
                {
                    ["branch"] = "East"
                }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var submitRes = await _client.SendAsync(submitMsg);

        Assert.Equal(HttpStatusCode.BadRequest, submitRes.StatusCode);
    }

    [Fact]
    public async Task NX_DATA_001_InvalidCheckboxItemType_ReturnsBadRequest()
    {
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync("InvCheck");
        var fields = new List<FormFieldDto>
        {
            new() { Id = "categories", Name = "categories", Label = "Danh mục", Type = FormFieldType.Checkbox, Required = true, Options = new() { "A", "B", "C" } }
        };
        var form = await CreatePublishedFormWithSchemaAsync(token, companyId, fields);

        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?>
                {
                    ["categories"] = new List<string> { "A", "Z" } // "Z" is not allowed
                }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var submitRes = await _client.SendAsync(submitMsg);

        Assert.Equal(HttpStatusCode.BadRequest, submitRes.StatusCode);
    }

    [Fact]
    public async Task NX_DATA_001_ValidNestedPayload_Succeeds()
    {
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync("ValidNest");
        var fields = new List<FormFieldDto>
        {
            new()
            {
                Id = "profile",
                Name = "profile",
                Label = "Hồ sơ cá nhân",
                Type = FormFieldType.Text,
                Fields = new List<FormFieldDto>
                {
                    new() { Id = "bio", Name = "bio", Label = "Tiểu sử", Type = FormFieldType.Text, Required = true },
                    new() { Id = "yearsExperience", Name = "yearsExperience", Label = "Số năm kinh nghiệm", Type = FormFieldType.Number, Required = true }
                }
            }
        };
        var form = await CreatePublishedFormWithSchemaAsync(token, companyId, fields);

        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?>
                {
                    ["profile"] = new Dictionary<string, object?>
                    {
                        ["bio"] = "Senior Engineer",
                        ["yearsExperience"] = 10
                    }
                }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var submitRes = await _client.SendAsync(submitMsg);

        Assert.Equal(HttpStatusCode.Created, submitRes.StatusCode);
    }

    [Fact]
    public async Task NX_DATA_001_ValidArrayPayload_Succeeds()
    {
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync("ValidArr");
        var fields = new List<FormFieldDto>
        {
            new()
            {
                Id = "items",
                Name = "items",
                Label = "Danh sách mục",
                Type = FormFieldType.Text,
                Fields = new List<FormFieldDto>
                {
                    new() { Id = "title", Name = "title", Label = "Tiêu đề", Type = FormFieldType.Text, Required = true },
                    new() { Id = "qty", Name = "qty", Label = "Số lượng", Type = FormFieldType.Number, Required = true }
                }
            }
        };
        var form = await CreatePublishedFormWithSchemaAsync(token, companyId, fields);

        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?>
                {
                    ["items"] = new List<Dictionary<string, object?>>
                    {
                        new() { ["title"] = "Laptop", ["qty"] = 1 },
                        new() { ["title"] = "Mouse", ["qty"] = 2 }
                    }
                }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var submitRes = await _client.SendAsync(submitMsg);

        Assert.Equal(HttpStatusCode.Created, submitRes.StatusCode);
    }

    [Fact]
    public async Task NX_DATA_001_MalformedJsonPayload_ReturnsBadRequest_NotInternalServerError()
    {
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync("Malformed");
        var fields = new List<FormFieldDto>
        {
            new() { Id = "text", Name = "text", Label = "Văn bản", Type = FormFieldType.Text, Required = true }
        };
        var form = await CreatePublishedFormWithSchemaAsync(token, companyId, fields);

        var rawInvalidJson = "{ \"data\": { \"text\": \"hello\", broken_json_syntax...";
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = new StringContent(rawInvalidJson, System.Text.Encoding.UTF8, "application/json")
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var submitRes = await _client.SendAsync(submitMsg);

        Assert.Equal(HttpStatusCode.BadRequest, submitRes.StatusCode);
        Assert.NotEqual(HttpStatusCode.InternalServerError, submitRes.StatusCode);
    }

    [Fact]
    public async Task NX_DATA_001_CrossTenantFormSubmission_ReturnsNotFoundOrForbidden()
    {
        var (tokenA, companyIdA, _) = await SetupCompanyAndOwnerAsync("TenantA");
        var (tokenB, companyIdB, _) = await SetupCompanyAndOwnerAsync("TenantB");

        var fields = new List<FormFieldDto>
        {
            new() { Id = "name", Name = "name", Label = "Tên", Type = FormFieldType.Text, Required = true }
        };
        var formA = await CreatePublishedFormWithSchemaAsync(tokenA, companyIdA, fields);

        // User B attempts to submit to Form of Company A using Company B's endpoint
        var crossMsg1 = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyIdB}/forms/{formA.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { ["name"] = "Attacker" }
            })
        };
        crossMsg1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var crossRes1 = await _client.SendAsync(crossMsg1);
        Assert.Equal(HttpStatusCode.NotFound, crossRes1.StatusCode);

        // User B attempts to submit directly to Company A's endpoint
        var crossMsg2 = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyIdA}/forms/{formA.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { ["name"] = "Attacker" }
            })
        };
        crossMsg2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var crossRes2 = await _client.SendAsync(crossMsg2);
        Assert.Equal(HttpStatusCode.Forbidden, crossRes2.StatusCode);
    }

    [Fact]
    public async Task NX_DATA_001_HistoricalSubmissions_WithExtraFields_RemainReadable()
    {
        var (token, companyId, userId) = await SetupCompanyAndOwnerAsync("HistoricalSub");
        var fields = new List<FormFieldDto>
        {
            new() { Id = "name", Name = "name", Label = "Tên", Type = FormFieldType.Text, Required = true }
        };
        var form = await CreatePublishedFormWithSchemaAsync(token, companyId, fields);

        // Insert historical submission directly with extra/legacy JSONB fields
        var submissionId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var historicalData = new Dictionary<string, object?>
            {
                ["name"] = "Old Employee",
                ["legacy_field_1"] = 12345,
                ["deprecated_metadata"] = "obsolete_value"
            };
            var sub = new FormSubmission
            {
                Id = submissionId,
                FormDefinitionId = form.Id,
                CompanyId = companyId,
                SubmittedById = userId,
                DataJson = JsonSerializer.Serialize(historicalData, JsonOptions),
                Status = SubmissionStatus.Approved,
                CurrentStepOrder = 1,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                UpdatedAt = DateTime.UtcNow.AddDays(-30)
            };
            db.FormSubmissions.Add(sub);
            await db.SaveChangesAsync();
        }

        // Query submission through API
        var getMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/submissions/{submissionId}");
        getMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var getRes = await _client.SendAsync(getMsg);

        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var subDto = await getRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.NotNull(subDto);
        Assert.Equal("Old Employee", subDto.Data["name"]?.ToString());
        Assert.True(subDto.Data.ContainsKey("legacy_field_1"));
    }

    [Fact]
    public async Task NX_DATA_001_RejectedPayload_DoesNotCreatePartialRecord()
    {
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync("NoPartialRec");
        var fields = new List<FormFieldDto>
        {
            new() { Id = "code", Name = "code", Label = "Mã", Type = FormFieldType.Text, Required = true }
        };
        var form = await CreatePublishedFormWithSchemaAsync(token, companyId, fields);

        int initialCount;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            initialCount = await db.FormSubmissions.CountAsync(s => s.CompanyId == companyId && s.FormDefinitionId == form.Id);
        }

        // Submit rejected payload (with undeclared field)
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?>
                {
                    ["code"] = "VALID",
                    ["unrecognizedField"] = "attack"
                }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var submitRes = await _client.SendAsync(submitMsg);
        Assert.Equal(HttpStatusCode.BadRequest, submitRes.StatusCode);

        // Verify count in database has not changed
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var currentCount = await db.FormSubmissions.CountAsync(s => s.CompanyId == companyId && s.FormDefinitionId == form.Id);
            Assert.Equal(initialCount, currentCount);
        }
    }

    #endregion

    #region NX-DATA-002: Published Form Schema Versioning

    [Fact]
    public async Task NX_DATA_002_NewSubmission_SnapshotsApplicableSchemaAndVersion()
    {
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync("SnapSchema");
        var fields = new List<FormFieldDto>
        {
            new() { Id = "score", Name = "score", Label = "Điểm", Type = FormFieldType.Number, Required = true },
            new() { Id = "notes", Name = "notes", Label = "Ghi chú", Type = FormFieldType.Text, Required = false }
        };
        var form = await CreatePublishedFormWithSchemaAsync(token, companyId, fields, "Evaluation Form");

        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { ["score"] = 95, ["notes"] = "Xuất sắc" }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var submitRes = await _client.SendAsync(submitMsg);
        Assert.Equal(HttpStatusCode.Created, submitRes.StatusCode);

        var subDto = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.NotNull(subDto);
        Assert.Equal(1, subDto.FormVersion);
        Assert.NotNull(subDto.SchemaSnapshot);
        Assert.Equal(2, subDto.SchemaSnapshot.Count);
        Assert.Contains(subDto.SchemaSnapshot, f => f.Name == "score");
        Assert.Contains(subDto.SchemaSnapshot, f => f.Name == "notes");
    }

    [Fact]
    public async Task NX_DATA_002_PublishedFormWithSubmissions_RejectsInPlaceSchemaModification()
    {
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync("RejectInPlace");
        var fields = new List<FormFieldDto>
        {
            new() { Id = "receiptUrl", Name = "receiptUrl", Label = "Hóa đơn", Type = FormFieldType.Text, Required = true },
            new() { Id = "amount", Name = "amount", Label = "Số tiền", Type = FormFieldType.Number, Required = true }
        };
        var form = await CreatePublishedFormWithSchemaAsync(token, companyId, fields, "Expense Report");

        // Create a submission
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { ["receiptUrl"] = "https://cdn.example.com/receipt.pdf", ["amount"] = 500 }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var submitRes = await _client.SendAsync(submitMsg);
        Assert.Equal(HttpStatusCode.Created, submitRes.StatusCode);

        // Attempt in-place modification of fields (deleting amount and adding budgetCategory)
        var alteredFields = new List<FormFieldDto>
        {
            new() { Id = "receiptUrl", Name = "receiptUrl", Label = "Hóa đơn", Type = FormFieldType.Text, Required = true },
            new() { Id = "budgetCategory", Name = "budgetCategory", Label = "Danh mục ngân sách", Type = FormFieldType.Select, Required = true, Options = new() { "Travel", "Office" } }
        };
        var updateMsg = new HttpRequestMessage(HttpMethod.Put, $"/companies/{companyId}/forms/{form.Id}")
        {
            Content = JsonContent.Create(new UpdateFormDto
            {
                Title = "Expense Report (Modified)",
                Fields = alteredFields
            })
        };
        updateMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var updateRes = await _client.SendAsync(updateMsg);

        Assert.Equal(HttpStatusCode.BadRequest, updateRes.StatusCode);
        var responseBody = await updateRes.Content.ReadAsStringAsync();
        Assert.Contains("Không thể chỉnh sửa cấu trúc trường", responseBody);
    }

    [Fact]
    public async Task NX_DATA_002_PublishedFormWithSubmissions_AllowsMetadataUpdate()
    {
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync("MetaUpdate");
        var fields = new List<FormFieldDto>
        {
            new() { Id = "feedback", Name = "feedback", Label = "Phản hồi", Type = FormFieldType.Text, Required = true }
        };
        var form = await CreatePublishedFormWithSchemaAsync(token, companyId, fields, "Survey 2026");

        // Submit once
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { ["feedback"] = "Great work!" }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var submitRes = await _client.SendAsync(submitMsg);
        Assert.Equal(HttpStatusCode.Created, submitRes.StatusCode);

        // Update metadata without altering fields (fields identical or null)
        var updateMetaMsg = new HttpRequestMessage(HttpMethod.Put, $"/companies/{companyId}/forms/{form.Id}")
        {
            Content = JsonContent.Create(new UpdateFormDto
            {
                Title = "Survey 2026 Updated Title",
                Description = "Updated survey description for all employees",
                Fields = fields // unchanged fields
            })
        };
        updateMetaMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var updateMetaRes = await _client.SendAsync(updateMetaMsg);

        Assert.Equal(HttpStatusCode.OK, updateMetaRes.StatusCode);
        var updatedForm = await updateMetaRes.Content.ReadFromJsonAsync<FormDefinitionDto>(JsonOptions);
        Assert.NotNull(updatedForm);
        Assert.Equal("Survey 2026 Updated Title", updatedForm.Title);
        Assert.Equal("Updated survey description for all employees", updatedForm.Description);
    }

    [Fact]
    public async Task NX_DATA_002_PublishedFormWithoutSubmissions_AllowsSchemaUpdate()
    {
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync("NoSubUpdate");
        var fields = new List<FormFieldDto>
        {
            new() { Id = "field1", Name = "field1", Label = "Trường 1", Type = FormFieldType.Text, Required = true }
        };
        var form = await CreatePublishedFormWithSchemaAsync(token, companyId, fields, "Zero Submissions Form");

        // Since 0 submissions exist, updating fields is permitted
        var newFields = new List<FormFieldDto>
        {
            new() { Id = "field1", Name = "field1", Label = "Trường 1", Type = FormFieldType.Text, Required = true },
            new() { Id = "field2", Name = "field2", Label = "Trường 2", Type = FormFieldType.Number, Required = false }
        };
        var updateMsg = new HttpRequestMessage(HttpMethod.Put, $"/companies/{companyId}/forms/{form.Id}")
        {
            Content = JsonContent.Create(new UpdateFormDto
            {
                Title = form.Title,
                Fields = newFields
            })
        };
        updateMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var updateRes = await _client.SendAsync(updateMsg);

        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updatedForm = await updateRes.Content.ReadFromJsonAsync<FormDefinitionDto>(JsonOptions);
        Assert.NotNull(updatedForm);
        Assert.Equal(2, updatedForm.Fields.Count);
        Assert.Equal(2, updatedForm.Version);
    }

    [Fact]
    public async Task NX_DATA_002_DraftForm_AllowsSchemaUpdate()
    {
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync("DraftUpdate");
        var createMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms")
        {
            Content = JsonContent.Create(new CreateFormDto
            {
                Title = "Draft Form Test",
                Code = "DFT_" + Guid.NewGuid().ToString("N")[..6],
                Fields = new List<FormFieldDto>
                {
                    new() { Id = "stepA", Name = "stepA", Label = "Bước A", Type = FormFieldType.Text, Required = true }
                }
            })
        };
        createMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createRes = await _client.SendAsync(createMsg);
        var form = await createRes.Content.ReadFromJsonAsync<FormDefinitionDto>(JsonOptions);

        var updateMsg = new HttpRequestMessage(HttpMethod.Put, $"/companies/{companyId}/forms/{form!.Id}")
        {
            Content = JsonContent.Create(new UpdateFormDto
            {
                Title = "Draft Form Test (Revised)",
                Fields = new List<FormFieldDto>
                {
                    new() { Id = "stepA", Name = "stepA", Label = "Bước A", Type = FormFieldType.Text, Required = true },
                    new() { Id = "stepB", Name = "stepB", Label = "Bước B", Type = FormFieldType.Text, Required = true }
                }
            })
        };
        updateMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var updateRes = await _client.SendAsync(updateMsg);

        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updatedForm = await updateRes.Content.ReadFromJsonAsync<FormDefinitionDto>(JsonOptions);
        Assert.NotNull(updatedForm);
        Assert.Equal(2, updatedForm.Fields.Count);
    }

    [Fact]
    public async Task NX_DATA_002_ArchivedForm_RejectsSchemaUpdate()
    {
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync("ArchivedUpdate");
        var fields = new List<FormFieldDto>
        {
            new() { Id = "oldData", Name = "oldData", Label = "Dữ liệu cũ", Type = FormFieldType.Text, Required = true }
        };
        var form = await CreatePublishedFormWithSchemaAsync(token, companyId, fields, "Archived Form Test");

        // Archive the form
        var archiveMsg = new HttpRequestMessage(HttpMethod.Patch, $"/companies/{companyId}/forms/{form.Id}/status")
        {
            Content = JsonContent.Create(new ChangeFormStatusDto { Status = FormStatus.Archived })
        };
        archiveMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var archiveRes = await _client.SendAsync(archiveMsg);
        Assert.Equal(HttpStatusCode.OK, archiveRes.StatusCode);

        // Attempt updating schema on archived form
        var updateMsg = new HttpRequestMessage(HttpMethod.Put, $"/companies/{companyId}/forms/{form.Id}")
        {
            Content = JsonContent.Create(new UpdateFormDto
            {
                Title = "Archived Form Attempt",
                Fields = new List<FormFieldDto>
                {
                    new() { Id = "newData", Name = "newData", Label = "Dữ liệu mới", Type = FormFieldType.Text, Required = true }
                }
            })
        };
        updateMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var updateRes = await _client.SendAsync(updateMsg);

        Assert.Equal(HttpStatusCode.BadRequest, updateRes.StatusCode);
    }

    [Fact]
    public async Task NX_DATA_002_ClonePublishedForm_CreatesNewDraftWithUniqueCode()
    {
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync("CloneForm");
        var fields = new List<FormFieldDto>
        {
            new() { Id = "item", Name = "item", Label = "Mặt hàng", Type = FormFieldType.Text, Required = true },
            new() { Id = "cost", Name = "cost", Label = "Chi phí", Type = FormFieldType.Number, Required = true }
        };
        var form = await CreatePublishedFormWithSchemaAsync(token, companyId, fields, "Procurement Request");

        // Clone the form
        var cloneMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/clone")
        {
            Content = JsonContent.Create(new CloneFormDto
            {
                NewTitle = "Procurement Request 2027",
                NewCode = "PROC_2027"
            })
        };
        cloneMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var cloneRes = await _client.SendAsync(cloneMsg);

        Assert.Equal(HttpStatusCode.Created, cloneRes.StatusCode);
        var clonedForm = await cloneRes.Content.ReadFromJsonAsync<FormDefinitionDto>(JsonOptions);
        Assert.NotNull(clonedForm);
        Assert.NotEqual(form.Id, clonedForm.Id);
        Assert.Equal("Procurement Request 2027", clonedForm.Title);
        Assert.Equal("PROC_2027", clonedForm.Code);
        Assert.Equal(FormStatus.Draft, clonedForm.Status);
        Assert.Equal(2, clonedForm.Fields.Count);
    }

    [Fact]
    public async Task NX_DATA_002_EditingClonedForm_DoesNotAlterHistoricalSubmissions()
    {
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync("HistoricalPreserve");
        var fieldsV1 = new List<FormFieldDto>
        {
            new() { Id = "legacyField", Name = "legacyField", Label = "Trường Cũ", Type = FormFieldType.Text, Required = true }
        };
        var formV1 = await CreatePublishedFormWithSchemaAsync(token, companyId, fieldsV1, "Original V1");

        // Submit S1 on formV1
        var submitV1Msg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{formV1.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { ["legacyField"] = "Giá trị lịch sử" }
            })
        };
        submitV1Msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var submitV1Res = await _client.SendAsync(submitV1Msg);
        Assert.Equal(HttpStatusCode.Created, submitV1Res.StatusCode);
        var subV1 = await submitV1Res.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        // Clone to create V2
        var cloneMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{formV1.Id}/clone")
        {
            Content = JsonContent.Create(new CloneFormDto { NewTitle = "Original V2" })
        };
        cloneMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var cloneRes = await _client.SendAsync(cloneMsg);
        var formV2 = await cloneRes.Content.ReadFromJsonAsync<FormDefinitionDto>(JsonOptions);

        // Update fields of draft V2
        var fieldsV2 = new List<FormFieldDto>
        {
            new() { Id = "newField", Name = "newField", Label = "Trường Mới", Type = FormFieldType.Text, Required = true }
        };
        var updateV2Msg = new HttpRequestMessage(HttpMethod.Put, $"/companies/{companyId}/forms/{formV2!.Id}")
        {
            Content = JsonContent.Create(new UpdateFormDto { Title = formV2.Title, Fields = fieldsV2 })
        };
        updateV2Msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await _client.SendAsync(updateV2Msg);

        // Publish formV2
        var pubV2Msg = new HttpRequestMessage(HttpMethod.Patch, $"/companies/{companyId}/forms/{formV2.Id}/status")
        {
            Content = JsonContent.Create(new ChangeFormStatusDto { Status = FormStatus.Published })
        };
        pubV2Msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await _client.SendAsync(pubV2Msg);

        // Submit S2 on formV2
        var submitV2Msg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{formV2.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { ["newField"] = "Giá trị mới" }
            })
        };
        submitV2Msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var submitV2Res = await _client.SendAsync(submitV2Msg);
        Assert.Equal(HttpStatusCode.Created, submitV2Res.StatusCode);
        var subV2 = await submitV2Res.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        // Verify S1 retains its original V1 schema snapshot
        var getS1Msg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/submissions/{subV1!.Id}");
        getS1Msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var getS1Res = await _client.SendAsync(getS1Msg);
        var retrievedS1 = await getS1Res.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.NotNull(retrievedS1);
        Assert.Single(retrievedS1.SchemaSnapshot);
        Assert.Equal("legacyField", retrievedS1.SchemaSnapshot[0].Name);

        // Verify S2 has its V2 schema snapshot
        var getS2Msg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/submissions/{subV2!.Id}");
        getS2Msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var getS2Res = await _client.SendAsync(getS2Msg);
        var retrievedS2 = await getS2Res.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.NotNull(retrievedS2);
        Assert.Single(retrievedS2.SchemaSnapshot);
        Assert.Equal("newField", retrievedS2.SchemaSnapshot[0].Name);
    }

    [Fact]
    public async Task NX_DATA_002_HistoricalSubmissionsWithoutSnapshot_FallbacksSafely()
    {
        var (token, companyId, userId) = await SetupCompanyAndOwnerAsync("LegacyFallback");
        var fields = new List<FormFieldDto>
        {
            new() { Id = "legacyCol", Name = "legacyCol", Label = "Cột cũ", Type = FormFieldType.Text, Required = true }
        };
        var form = await CreatePublishedFormWithSchemaAsync(token, companyId, fields, "Legacy Form");

        // Manually insert submission without SchemaSnapshotJson
        var subId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var sub = new FormSubmission
            {
                Id = subId,
                FormDefinitionId = form.Id,
                CompanyId = companyId,
                SubmittedById = userId,
                DataJson = JsonSerializer.Serialize(new Dictionary<string, object?> { ["legacyCol"] = "Sample" }, JsonOptions),
                Status = SubmissionStatus.Approved,
                FormVersion = 1,
                SchemaSnapshotJson = null, // Historical record prior to snapshotting
                CreatedAt = DateTime.UtcNow.AddMonths(-3),
                UpdatedAt = DateTime.UtcNow.AddMonths(-3)
            };
            db.FormSubmissions.Add(sub);
            await db.SaveChangesAsync();
        }

        // Query submission through API
        var getMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/submissions/{subId}");
        getMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var getRes = await _client.SendAsync(getMsg);

        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var subDto = await getRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.NotNull(subDto);
        Assert.NotNull(subDto.SchemaSnapshot);
        Assert.Single(subDto.SchemaSnapshot);
        Assert.Equal("legacyCol", subDto.SchemaSnapshot[0].Name);
    }

    [Fact]
    public async Task NX_DATA_002_CrossTenantCloneOrAccess_IsRejected()
    {
        var (tokenA, companyIdA, _) = await SetupCompanyAndOwnerAsync("TenantAForms");
        var (tokenB, companyIdB, _) = await SetupCompanyAndOwnerAsync("TenantBForms");

        var fields = new List<FormFieldDto>
        {
            new() { Id = "secretFormula", Name = "secretFormula", Label = "Bí mật", Type = FormFieldType.Text, Required = true }
        };
        var formA = await CreatePublishedFormWithSchemaAsync(tokenA, companyIdA, fields, "Confidential Form A");

        // User B attempts to clone Company A's form via Company B endpoint
        var cloneCrossMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyIdB}/forms/{formA.Id}/clone")
        {
            Content = JsonContent.Create(new CloneFormDto { NewTitle = "Stolen Form" })
        };
        cloneCrossMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var cloneCrossRes = await _client.SendAsync(cloneCrossMsg);
        Assert.Equal(HttpStatusCode.NotFound, cloneCrossRes.StatusCode);

        // User B attempts to clone directly via Company A endpoint
        var cloneDirectMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyIdA}/forms/{formA.Id}/clone")
        {
            Content = JsonContent.Create(new CloneFormDto { NewTitle = "Stolen Direct Form" })
        };
        cloneDirectMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var cloneDirectRes = await _client.SendAsync(cloneDirectMsg);
        Assert.Equal(HttpStatusCode.Forbidden, cloneDirectRes.StatusCode);
    }

    #endregion

    #region Sprint 2 Cross-Module Compatibility & End-to-End Workflows

    [Fact]
    public async Task Sprint2_DepartmentReparenting_PreservesWorkflowDepartmentApproverResolution()
    {
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync("DeptWf");
        var (mgrToken, mgrUserId, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "MgrDept");
        var (memberToken, memberUserId, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member, "MemDept");

        // Create Parent Department and Child Department with Manager
        var parentDept = await CreateDepartmentAsync(ownerToken, companyId, "Headquarters");
        var childDeptReq = new CreateDepartmentRequest
        {
            Name = "Engineering",
            ParentId = parentDept.Id,
            ManagerId = mgrUserId
        };
        var childDeptMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/departments")
        {
            Content = JsonContent.Create(childDeptReq)
        };
        childDeptMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var childDeptRes = await _client.SendAsync(childDeptMsg);
        var childDept = await childDeptRes.Content.ReadFromJsonAsync<DepartmentResponse>(JsonOptions);

        // Reparent Engineering to root using Guid.Empty
        var reparentMsg = new HttpRequestMessage(HttpMethod.Patch, $"/departments/{childDept!.Id}")
        {
            Content = JsonContent.Create(new UpdateDepartmentRequest
            {
                Name = "Engineering Autonomous",
                ParentId = Guid.Empty,
                ManagerId = mgrUserId
            })
        };
        reparentMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var reparentRes = await _client.SendAsync(reparentMsg);
        Assert.Equal(HttpStatusCode.OK, reparentRes.StatusCode);

        // Create form assigned to Engineering department with DepartmentManager approval step
        var createFormMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms")
        {
            Content = JsonContent.Create(new CreateFormDto
            {
                Title = "Eng Purchase Request",
                Code = "ENG_PR_" + Guid.NewGuid().ToString("N")[..6],
                DepartmentId = childDept.Id,
                Fields = new List<FormFieldDto>
                {
                    new() { Id = "toolName", Name = "toolName", Label = "Tên công cụ", Type = FormFieldType.Text, Required = true }
                }
            })
        };
        createFormMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var formRes = await _client.SendAsync(createFormMsg);
        var form = await formRes.Content.ReadFromJsonAsync<FormDefinitionDto>(JsonOptions);

        // Publish Form
        var pubMsg = new HttpRequestMessage(HttpMethod.Patch, $"/companies/{companyId}/forms/{form!.Id}/status")
        {
            Content = JsonContent.Create(new ChangeFormStatusDto { Status = FormStatus.Published })
        };
        pubMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        await _client.SendAsync(pubMsg);

        // Configure workflow with DepartmentManager step
        var wfMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/workflow")
        {
            Content = JsonContent.Create(new CreateOrUpdateWorkflowDto
            {
                Name = "Eng Dept Approval",
                IsActive = true,
                Steps = new List<CreateOrUpdateApprovalStepDto>
                {
                    new()
                    {
                        OrderIndex = 1,
                        Name = "Manager Approval",
                        StepType = ApprovalStepType.DepartmentManager,
                        AllowSelfApproval = false
                    }
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
                Data = new Dictionary<string, object?> { ["toolName"] = "JetBrains Rider" }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var submitRes = await _client.SendAsync(submitMsg);
        Assert.Equal(HttpStatusCode.Created, submitRes.StatusCode);
        var subDto = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        // Department manager approves the step -> Succeeds
        var approveMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{subDto!.Id}/steps/1/process")
        {
            Content = JsonContent.Create(new StepApprovalActionDto
            {
                Status = ApprovalStepStatus.Approved,
                ReviewNotes = "Approved by autonomous department manager"
            })
        };
        approveMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mgrToken);
        var approveRes = await _client.SendAsync(approveMsg);
        Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);

        // Check submission is now Approved
        var getMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/submissions/{subDto.Id}");
        getMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var getRes = await _client.SendAsync(getMsg);
        var finalSub = await getRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.Equal(SubmissionStatus.Approved, finalSub!.Status);
    }

    [Fact]
    public async Task Sprint2_SubmissionWorkflowLifecycle_PreservesSchemaSnapshotAcrossAllApprovalSteps()
    {
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync("SnapWfLifecycle");
        var (mgrToken, _, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "MgrSnap");
        var (memToken, _, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member, "MemSnap");

        var fields = new List<FormFieldDto>
        {
            new() { Id = "projectTitle", Name = "projectTitle", Label = "Dự án", Type = FormFieldType.Text, Required = true },
            new() { Id = "estimatedBudget", Name = "estimatedBudget", Label = "Ngân sách", Type = FormFieldType.Number, Required = true }
        };
        var form = await CreatePublishedFormWithSchemaAsync(ownerToken, companyId, fields, "Project Proposal");

        // Setup 2-step workflow: Step 1 (Manager Role), Step 2 (CompanyOwner StepType)
        var wfMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/workflow")
        {
            Content = JsonContent.Create(new CreateOrUpdateWorkflowDto
            {
                Name = "Two Step Sequential",
                IsActive = true,
                Steps = new List<CreateOrUpdateApprovalStepDto>
                {
                    new() { OrderIndex = 1, Name = "Manager Review", StepType = ApprovalStepType.Role, ApproverRole = MemberRole.Manager, AllowSelfApproval = false },
                    new() { OrderIndex = 2, Name = "Owner Approval", StepType = ApprovalStepType.CompanyOwner, AllowSelfApproval = false }
                }
            })
        };
        wfMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        await _client.SendAsync(wfMsg);

        // Submitter submits proposal
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { ["projectTitle"] = "AI Infrastructure", ["estimatedBudget"] = 50000 }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memToken);
        var submitRes = await _client.SendAsync(submitMsg);
        Assert.Equal(HttpStatusCode.Created, submitRes.StatusCode);
        var subDto = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        Assert.Equal(1, subDto!.FormVersion);
        Assert.Equal(2, subDto.SchemaSnapshot.Count);
        Assert.Equal(SubmissionStatus.InReview, subDto.Status);

        // Step 1: Manager approves
        var step1Msg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{subDto.Id}/steps/1/process")
        {
            Content = JsonContent.Create(new StepApprovalActionDto { Status = ApprovalStepStatus.Approved, ReviewNotes = "Manager Approved" })
        };
        step1Msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mgrToken);
        var step1Res = await _client.SendAsync(step1Msg);
        Assert.Equal(HttpStatusCode.OK, step1Res.StatusCode);

        // Verify after Step 1: SchemaSnapshot and FormVersion remain intact
        var getStep1Msg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/submissions/{subDto.Id}");
        getStep1Msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memToken);
        var getStep1Res = await _client.SendAsync(getStep1Msg);
        var subAfterStep1 = await getStep1Res.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.Equal(1, subAfterStep1!.FormVersion);
        Assert.Equal(2, subAfterStep1.SchemaSnapshot.Count);
        Assert.Equal(2, subAfterStep1.CurrentStepOrder);

        // Step 2: Owner approves
        var step2Msg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{subDto.Id}/steps/2/process")
        {
            Content = JsonContent.Create(new StepApprovalActionDto { Status = ApprovalStepStatus.Approved, ReviewNotes = "Owner Approved" })
        };
        step2Msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var step2Res = await _client.SendAsync(step2Msg);
        Assert.Equal(HttpStatusCode.OK, step2Res.StatusCode);

        // Verify terminal state: Submission is Approved, SchemaSnapshot is permanently preserved
        var finalGetMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/submissions/{subDto.Id}");
        finalGetMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memToken);
        var finalGetRes = await _client.SendAsync(finalGetMsg);
        var finalSub = await finalGetRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.Equal(SubmissionStatus.Approved, finalSub!.Status);
        Assert.Equal(1, finalSub.FormVersion);
        Assert.Equal(2, finalSub.SchemaSnapshot.Count);
        Assert.Contains(finalSub.SchemaSnapshot, f => f.Name == "projectTitle");
        Assert.Contains(finalSub.SchemaSnapshot, f => f.Name == "estimatedBudget");
    }

    [Fact]
    public async Task Sprint2_ClonedForm_CanAttachNewWorkflow_IndependentOfOriginalForm()
    {
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync("CloneWfIndep");
        var (memToken, _, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member, "MemCloneWf");

        // Form 1 with 1-step workflow
        var (form1Id, _) = await CreateFormWithWorkflowAsync(ownerToken, companyId, new List<CreateOrUpdateApprovalStepDto>
        {
            new() { OrderIndex = 1, Name = "Quick Review", StepType = ApprovalStepType.CompanyOwner }
        });

        // Submit S1 to Form 1
        var sub1Msg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form1Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto { Data = new Dictionary<string, object?> { ["notes"] = "S1 on Form 1" } })
        };
        sub1Msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memToken);
        var sub1Res = await _client.SendAsync(sub1Msg);
        var sub1 = await sub1Res.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.Single(sub1!.ApprovalSteps);

        // Clone Form 1 to Form 2
        var cloneMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form1Id}/clone")
        {
            Content = JsonContent.Create(new CloneFormDto { NewTitle = "Form 2 with Advanced Workflow" })
        };
        cloneMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var cloneRes = await _client.SendAsync(cloneMsg);
        var form2 = await cloneRes.Content.ReadFromJsonAsync<FormDefinitionDto>(JsonOptions);

        // Publish Form 2
        var pub2Msg = new HttpRequestMessage(HttpMethod.Patch, $"/companies/{companyId}/forms/{form2!.Id}/status")
        {
            Content = JsonContent.Create(new ChangeFormStatusDto { Status = FormStatus.Published })
        };
        pub2Msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        await _client.SendAsync(pub2Msg);

        // Attach new 2-step workflow to Form 2
        var wf2Msg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form2.Id}/workflow")
        {
            Content = JsonContent.Create(new CreateOrUpdateWorkflowDto
            {
                Name = "Advanced Workflow Form 2",
                IsActive = true,
                Steps = new List<CreateOrUpdateApprovalStepDto>
                {
                    new() { OrderIndex = 1, Name = "Step 1", StepType = ApprovalStepType.CompanyOwner },
                    new() { OrderIndex = 2, Name = "Step 2", StepType = ApprovalStepType.CompanyOwner }
                }
            })
        };
        wf2Msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        await _client.SendAsync(wf2Msg);

        // Submit S2 to Form 2
        var sub2Msg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form2.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto { Data = new Dictionary<string, object?> { ["notes"] = "S2 on Form 2" } })
        };
        sub2Msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memToken);
        var sub2Res = await _client.SendAsync(sub2Msg);
        var sub2 = await sub2Res.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        // Assert S2 has 2 steps while S1 still has 1 step
        Assert.Equal(2, sub2!.ApprovalSteps.Count);
        Assert.Single(sub1.ApprovalSteps);
    }

    [Fact]
    public async Task Sprint2_DelegatedStep_AdvancedByDelegate_UpdatesSubmissionStatusCorrectly()
    {
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync("DelegateAdvance");
        var (mgr1Token, mgr1UserId, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "Mgr1Del");
        var (mgr2Token, mgr2UserId, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "Mgr2Del");
        var (memToken, _, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member, "MemDel");

        var (formId, _) = await CreateFormWithWorkflowAsync(ownerToken, companyId, new List<CreateOrUpdateApprovalStepDto>
        {
            new()
            {
                OrderIndex = 1,
                Name = "Manager Initial Step",
                StepType = ApprovalStepType.SpecificUser,
                SpecificApproverUserId = mgr1UserId,
                AllowSelfApproval = false
            }
        });

        // Submit
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{formId}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto { Data = new Dictionary<string, object?> { ["notes"] = "Delegation workflow test" } })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memToken);
        var submitRes = await _client.SendAsync(submitMsg);
        var subDto = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        // Manager 1 delegates step 1 to Manager 2
        var delMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{subDto!.Id}/steps/1/delegate")
        {
            Content = JsonContent.Create(new DelegateStepDto { NewApproverUserId = mgr2UserId, Reason = "Leave of absence" })
        };
        delMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mgr1Token);
        var delRes = await _client.SendAsync(delMsg);
        Assert.Equal(HttpStatusCode.OK, delRes.StatusCode);

        // Manager 2 processes the approval
        var procMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{subDto.Id}/steps/1/process")
        {
            Content = JsonContent.Create(new StepApprovalActionDto { Status = ApprovalStepStatus.Approved, ReviewNotes = "Approved as delegated substitute" })
        };
        procMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mgr2Token);
        var procRes = await _client.SendAsync(procMsg);
        Assert.Equal(HttpStatusCode.OK, procRes.StatusCode);

        // Submitter queries submission -> Status Approved, ReviewNotes populated
        var getMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/submissions/{subDto.Id}");
        getMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memToken);
        var getRes = await _client.SendAsync(getMsg);
        var finalSub = await getRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.Equal(SubmissionStatus.Approved, finalSub!.Status);
        Assert.Equal("Approved as delegated substitute", finalSub.ApprovalSteps[0].ReviewNotes);
        Assert.Equal(mgr2UserId, finalSub.ApprovalSteps[0].ReviewedById);
    }

    [Fact]
    public async Task Sprint2_FormUpdateMetadata_WhileSubmissionsInReview_PreservesInReviewWorkflows()
    {
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync("MetaPreserveWf");
        var (mgrToken, _, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "MgrMetaPreserve");
        var (memToken, _, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member, "MemMetaPreserve");

        var fields = new List<FormFieldDto>
        {
            new() { Id = "claimAmount", Name = "claimAmount", Label = "Số tiền yêu cầu", Type = FormFieldType.Number, Required = true }
        };
        var form = await CreatePublishedFormWithSchemaAsync(ownerToken, companyId, fields, "Medical Claim");

        // 1-step workflow
        var wfMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/workflow")
        {
            Content = JsonContent.Create(new CreateOrUpdateWorkflowDto
            {
                Name = "Medical Claim Workflow",
                IsActive = true,
                Steps = new List<CreateOrUpdateApprovalStepDto>
                {
                    new() { OrderIndex = 1, Name = "HR Approval", StepType = ApprovalStepType.Role, ApproverRole = MemberRole.Manager }
                }
            })
        };
        wfMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        await _client.SendAsync(wfMsg);

        // Submit form
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto { Data = new Dictionary<string, object?> { ["claimAmount"] = 1200 } })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memToken);
        var submitRes = await _client.SendAsync(submitMsg);
        var subDto = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.Equal(SubmissionStatus.InReview, subDto!.Status);

        // While submission is InReview, admin updates form title & description
        var updateMetaMsg = new HttpRequestMessage(HttpMethod.Put, $"/companies/{companyId}/forms/{form.Id}")
        {
            Content = JsonContent.Create(new UpdateFormDto
            {
                Title = "Medical Claim 2026 Policy Updated",
                Description = "Updated medical claim policy description",
                Fields = fields
            })
        };
        updateMetaMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var updateMetaRes = await _client.SendAsync(updateMetaMsg);
        Assert.Equal(HttpStatusCode.OK, updateMetaRes.StatusCode);

        // Manager successfully approves the in-review submission
        var approveMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{subDto.Id}/steps/1/process")
        {
            Content = JsonContent.Create(new StepApprovalActionDto { Status = ApprovalStepStatus.Approved, ReviewNotes = "Medical claim approved under updated policy" })
        };
        approveMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mgrToken);
        var approveRes = await _client.SendAsync(approveMsg);
        Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);

        // Final verification
        var getMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/submissions/{subDto.Id}");
        getMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memToken);
        var getRes = await _client.SendAsync(getMsg);
        var finalSub = await getRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.Equal(SubmissionStatus.Approved, finalSub!.Status);
        Assert.Equal(1, finalSub.FormVersion);
        Assert.Single(finalSub.SchemaSnapshot);
        Assert.Equal("claimAmount", finalSub.SchemaSnapshot[0].Name);
    }

    #endregion

    #region Checkpoint 6 & 7: Stress, Concurrency, Telemetry & Production Sign-off

    [Fact]
    public async Task Sprint2_DeepDepartmentHierarchy_CycleCheckStress_ExecutionWithinThreshold()
    {
        var (token, companyId) = await SetupCompanyAsync("StressDept");

        // Construct a deep 15-level department chain: Dept0 -> Dept1 -> Dept2 -> ... -> Dept14
        const int depth = 15;
        var depts = new List<DepartmentResponse>();
        Guid? parentId = null;
        for (int i = 0; i < depth; i++)
        {
            var dept = await CreateDepartmentAsync(token, companyId, $"Tier {i} Department", parentId);
            depts.Add(dept);
            parentId = dept.Id;
        }

        // Stress check: Attempt to create a 15-level cycle by setting Dept0.ParentId = Dept14.Id
        var sw = Stopwatch.StartNew();
        var cycleReq = new UpdateDepartmentRequest { ParentId = depts[^1].Id };
        var cycleMsg = new HttpRequestMessage(HttpMethod.Patch, $"/departments/{depts[0].Id}")
        {
            Content = JsonContent.Create(cycleReq)
        };
        cycleMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var cycleRes = await _client.SendAsync(cycleMsg);
        sw.Stop();

        Assert.Equal(HttpStatusCode.BadRequest, cycleRes.StatusCode);
        var errBody = await cycleRes.Content.ReadAsStringAsync();
        Assert.Contains("chu trình lặp", errBody);
        Assert.True(sw.ElapsedMilliseconds < 1000, $"Cycle detection took {sw.ElapsedMilliseconds}ms, exceeding threshold");

        // Verify GetDepartmentTree on deep chain executes safely without stack overflow
        var treeMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/departments?tree=true");
        treeMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var treeRes = await _client.SendAsync(treeMsg);
        Assert.Equal(HttpStatusCode.OK, treeRes.StatusCode);
        var tree = await treeRes.Content.ReadFromJsonAsync<List<DepartmentTreeNodeResponse>>(JsonOptions);
        Assert.NotNull(tree);
        Assert.Single(tree); // Exactly 1 root node at top
        Assert.Equal(depts[0].Id, tree[0].Id);
        Assert.Single(tree[0].Children); // Has 1 child
    }

    [Fact]
    public async Task Sprint2_HighVolumeSubmissionPayload_ComplexNestedRepeatingSections_ValidatesAccurately()
    {
        var (token, companyId) = await SetupCompanyAsync("ComplexPayload");

        var fields = new List<FormFieldDto>
        {
            new() { Id = "projectName", Name = "projectName", Label = "Project Name", Type = FormFieldType.Text, Required = true },
            new() { Id = "budget", Name = "budget", Label = "Total Budget", Type = FormFieldType.Number, Required = true },
            new() {
                Id = "contact",
                Name = "contact",
                Label = "Contact Information",
                Type = FormFieldType.Text,
                Required = true,
                Fields = new List<FormFieldDto>
                {
                    new() { Id = "contactName", Name = "contactName", Label = "Contact Name", Type = FormFieldType.Text, Required = true },
                    new() { Id = "contactEmail", Name = "contactEmail", Label = "Contact Email", Type = FormFieldType.Email, Required = true }
                }
            },
            new() {
                Id = "milestones",
                Name = "milestones",
                Label = "Project Milestones",
                Type = FormFieldType.Text,
                Required = true,
                Fields = new List<FormFieldDto>
                {
                    new() { Id = "milestoneName", Name = "milestoneName", Label = "Milestone Name", Type = FormFieldType.Text, Required = true },
                    new() { Id = "cost", Name = "cost", Label = "Cost", Type = FormFieldType.Number, Required = true }
                }
            }
        };

        var form = await CreatePublishedFormWithSchemaAsync(token, companyId, fields, "Enterprise Project Intake");

        // Construct payload with 10 repeating milestones
        var milestones = new List<Dictionary<string, object?>>();
        for (int i = 1; i <= 10; i++)
        {
            milestones.Add(new Dictionary<string, object?>
            {
                ["milestoneName"] = $"Phase {i} Deliverables",
                ["cost"] = 5000 * i
            });
        }

        var validPayload = new Dictionary<string, object?>
        {
            ["projectName"] = "Cloud Migration 2026",
            ["budget"] = 275000,
            ["contact"] = new Dictionary<string, object?>
            {
                ["contactName"] = "Nguyen Van A",
                ["contactEmail"] = "nguyen.a@nexaops.com"
            },
            ["milestones"] = milestones
        };

        var sw = Stopwatch.StartNew();
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto { Data = validPayload })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var submitRes = await _client.SendAsync(submitMsg);
        sw.Stop();

        Assert.Equal(HttpStatusCode.Created, submitRes.StatusCode);
        Assert.True(sw.ElapsedMilliseconds < 2000, $"Payload validation & submission took {sw.ElapsedMilliseconds}ms");

        var subDto = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.NotNull(subDto);
        Assert.Equal(1, subDto.FormVersion);
        Assert.NotNull(subDto.SchemaSnapshot);
        Assert.Equal(4, subDto.SchemaSnapshot.Count);

        // Verify reading back from DB preserves full schema snapshot subfields and repeating data
        var getMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/submissions/{subDto.Id}");
        getMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var getRes = await _client.SendAsync(getMsg);
        var fetchedSub = await getRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.NotNull(fetchedSub);
        var contactField = fetchedSub.SchemaSnapshot.First(f => f.Name == "contact");
        Assert.NotNull(contactField.Fields);
        Assert.Equal(2, contactField.Fields.Count);
        var milestonesField = fetchedSub.SchemaSnapshot.First(f => f.Name == "milestones");
        Assert.NotNull(milestonesField.Fields);
        Assert.Equal(2, milestonesField.Fields.Count);
    }

    [Fact]
    public async Task Sprint2_RapidConcurrentSubmissions_PreservesVersionAndDataIntegrity()
    {
        var (token, companyId) = await SetupCompanyAsync("ConcurrentSub");

        var fields = new List<FormFieldDto>
        {
            new() { Id = "ticketTitle", Name = "ticketTitle", Label = "Ticket Title", Type = FormFieldType.Text, Required = true },
            new() { Id = "priorityLevel", Name = "priorityLevel", Label = "Priority Level", Type = FormFieldType.Number, Required = true }
        };

        var form = await CreatePublishedFormWithSchemaAsync(token, companyId, fields, "Incident Report");

        // Fire 5 concurrent submissions simultaneously
        var tasks = Enumerable.Range(1, 5).Select(async i =>
        {
            var msg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
            {
                Content = JsonContent.Create(new SubmitFormDto
                {
                    Data = new Dictionary<string, object?>
                    {
                        ["ticketTitle"] = $"Incident #{i}",
                        ["priorityLevel"] = i
                    }
                })
            };
            msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var res = await _client.SendAsync(msg);
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            return await res.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        }).ToList();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(5, results.Length);
        var uniqueIds = results.Select(r => r!.Id).Distinct().Count();
        Assert.Equal(5, uniqueIds); // All submissions have unique IDs

        foreach (var sub in results)
        {
            Assert.NotNull(sub);
            Assert.Equal(1, sub.FormVersion);
            Assert.Equal(2, sub.SchemaSnapshot.Count);
            Assert.Equal(SubmissionStatus.Pending, sub.Status);
        }
    }

    [Fact]
    public async Task Sprint2_SecurityTelemetry_PayloadErrorDoesNotLeakInternalStackOrSensitiveData()
    {
        var (token, companyId) = await SetupCompanyAsync("TelemetrySec");

        var fields = new List<FormFieldDto>
        {
            new() { Id = "employeeAge", Name = "employeeAge", Label = "Employee Age", Type = FormFieldType.Number, Required = true }
        };

        var form = await CreatePublishedFormWithSchemaAsync(token, companyId, fields, "Employee Onboarding");

        // 1. Send invalid type payload (string for number)
        var invalidTypeMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { ["employeeAge"] = "not_a_valid_integer" }
            })
        };
        invalidTypeMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var invalidTypeRes = await _client.SendAsync(invalidTypeMsg);
        Assert.Equal(HttpStatusCode.BadRequest, invalidTypeRes.StatusCode);
        var invalidTypeBody = await invalidTypeRes.Content.ReadAsStringAsync();

        // 2. Send undeclared property payload
        var undeclaredMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { ["employeeAge"] = 28, ["sqlInjectionPayload"] = "DROP TABLE users;" }
            })
        };
        undeclaredMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var undeclaredRes = await _client.SendAsync(undeclaredMsg);
        Assert.Equal(HttpStatusCode.BadRequest, undeclaredRes.StatusCode);
        var undeclaredBody = await undeclaredRes.Content.ReadAsStringAsync();

        // Assert no stack trace, server file paths, or sensitive runtime internals leak in either response
        foreach (var body in new[] { invalidTypeBody, undeclaredBody })
        {
            Assert.DoesNotContain("System.Exception", body);
            Assert.DoesNotContain("at NexaOps.", body);
            Assert.DoesNotContain("at Microsoft.AspNetCore", body);
            Assert.DoesNotContain("PostgresException", body);
            Assert.DoesNotContain("Server=", body);
            Assert.DoesNotContain("Password=", body);
        }

        // Verify both bodies convey safe business error context
        Assert.True(invalidTypeBody.Contains("employeeAge") || invalidTypeBody.Contains("số"));
        Assert.True(undeclaredBody.Contains("sqlInjectionPayload") || undeclaredBody.Contains("không được định nghĩa"));
    }

    [Fact]
    public async Task Sprint2_AuditTrail_RecordsRemediatedDelegationAction()
    {
        var (ownerToken, companyId, ownerUserId) = await SetupCompanyAndOwnerAsync("AuditSec");
        var (mgrToken, _, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager, "mgr");
        var (empToken, _, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member, "emp");

        // 1-step workflow assigned to Manager
        var fields = new List<FormFieldDto>
        {
            new() { Id = "requestReason", Name = "requestReason", Label = "Reason", Type = FormFieldType.Text, Required = true }
        };
        var form = await CreatePublishedFormWithSchemaAsync(ownerToken, companyId, fields, "Purchase Request");

        var wfMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/workflow")
        {
            Content = JsonContent.Create(new CreateOrUpdateWorkflowDto
            {
                Name = "Purchase Approval Workflow",
                IsActive = true,
                Steps = new List<CreateOrUpdateApprovalStepDto>
                {
                    new() { OrderIndex = 1, Name = "Manager Step", StepType = ApprovalStepType.Role, ApproverRole = MemberRole.Manager }
                }
            })
        };
        wfMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        await _client.SendAsync(wfMsg);

        // Member submits form
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto { Data = new Dictionary<string, object?> { ["requestReason"] = "Office Hardware" } })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", empToken);
        var submitRes = await _client.SendAsync(submitMsg);
        var subDto = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.NotNull(subDto);

        // Manager delegates step 1 to Owner (active company owner)
        var delegateMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{subDto.Id}/steps/1/delegate")
        {
            Content = JsonContent.Create(new DelegateStepDto
            {
                NewApproverUserId = ownerUserId,
                Reason = "Delegating to company owner during executive audit"
            })
        };
        delegateMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mgrToken);
        var delegateRes = await _client.SendAsync(delegateMsg);
        Assert.Equal(HttpStatusCode.OK, delegateRes.StatusCode);

        // Owner inspects company audit logs
        var auditMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/audit-logs?action=DELEGATE_STEP");
        auditMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var auditRes = await _client.SendAsync(auditMsg);
        Assert.Equal(HttpStatusCode.OK, auditRes.StatusCode);

        var auditLogs = await auditRes.Content.ReadFromJsonAsync<PagedResult<AuditLogDto>>(JsonOptions);
        Assert.NotNull(auditLogs);
        Assert.NotEmpty(auditLogs.Items);

        var delegateLog = auditLogs.Items.FirstOrDefault(a => a.Action == "DELEGATE_STEP" && a.EntityId == subDto.Id.ToString());
        Assert.NotNull(delegateLog);
        Assert.Equal("FormSubmission", delegateLog.EntityType);
        Assert.Equal(companyId, delegateLog.CompanyId);
    }

    #endregion
}
