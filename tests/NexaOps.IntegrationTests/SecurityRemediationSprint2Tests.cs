using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexaOps.Application.Auth.DTOs;
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
}
