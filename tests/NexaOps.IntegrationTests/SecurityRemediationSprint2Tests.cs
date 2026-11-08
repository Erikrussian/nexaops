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
}
