using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NexaOps.Application.Auth.DTOs;
using NexaOps.Application.Companies.DTOs;
using NexaOps.Application.Departments.DTOs;
using NexaOps.IntegrationTests.Common;
using Xunit;

namespace NexaOps.IntegrationTests;

public class SecurityRemediationSprint2Tests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SecurityRemediationSprint2Tests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    #region Helpers

    private async Task<(string Token, Guid CompanyId)> SetupCompanyAsync(string prefix = "DeptSec")
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

        return (token, company!.Id);
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
}

