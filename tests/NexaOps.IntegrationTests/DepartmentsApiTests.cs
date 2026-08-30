using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NexaOps.Application.Auth.DTOs;
using NexaOps.Application.Companies.DTOs;
using NexaOps.Application.Departments.DTOs;
using NexaOps.IntegrationTests.Common;

namespace NexaOps.IntegrationTests;

public class DepartmentsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DepartmentsApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string Token, Guid CompanyId)> SetupCompanyAsync()
    {
        var tag = Guid.NewGuid().ToString("N");
        var registerRequest = new RegisterRequest
        {
            Name = $"Dept Owner {tag}",
            Email = $"deptowner.{tag}@nexaops.com",
            Password = "password123"
        };
        var regRes = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        var authResult = await regRes.Content.ReadFromJsonAsync<AuthResponse>();
        var token = authResult!.AccessToken;

        var compRequest = new CreateCompanyRequest { Name = $"Dept Company {tag}" };
        var msgComp = new HttpRequestMessage(HttpMethod.Post, "/companies")
        {
            Content = JsonContent.Create(compRequest)
        };
        msgComp.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var compRes = await _client.SendAsync(msgComp);
        var company = await compRes.Content.ReadFromJsonAsync<CompanyResponse>();

        return (token, company!.Id);
    }

    [Fact]
    public async Task CreateDepartment_ValidPayload_Returns201Created()
    {
        // Arrange
        var (token, companyId) = await SetupCompanyAsync();

        var request = new CreateDepartmentRequest
        {
            Name = "Phòng Kỹ Thuật",
            Code = "TECH"
        };

        var msg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/departments")
        {
            Content = JsonContent.Create(request)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(msg);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dept = await response.Content.ReadFromJsonAsync<DepartmentResponse>();
        Assert.NotNull(dept);
        Assert.Equal("Phòng Kỹ Thuật", dept.Name);
        Assert.Equal("TECH", dept.Code);
        Assert.Equal(companyId, dept.CompanyId);
    }

    [Fact]
    public async Task CreateDepartment_DuplicateNameInSameCompany_Returns400BadRequest()
    {
        // Arrange
        var (token, companyId) = await SetupCompanyAsync();

        var request1 = new CreateDepartmentRequest { Name = "Phòng Nhân Sự" };
        var msg1 = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/departments")
        {
            Content = JsonContent.Create(request1)
        };
        msg1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res1 = await _client.SendAsync(msg1);
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

        // Act - Duplicate name
        var request2 = new CreateDepartmentRequest { Name = "Phòng Nhân Sự" };
        var msg2 = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/departments")
        {
            Content = JsonContent.Create(request2)
        };
        msg2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res2 = await _client.SendAsync(msg2);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, res2.StatusCode);
    }

    [Fact]
    public async Task CreateDepartment_NonExistentParent_Returns404NotFound()
    {
        // Arrange
        var (token, companyId) = await SetupCompanyAsync();

        var request = new CreateDepartmentRequest
        {
            Name = "Phòng Con",
            ParentId = Guid.NewGuid() // Non-existent
        };

        var msg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/departments")
        {
            Content = JsonContent.Create(request)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(msg);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDepartments_TreeMode_ReturnsNestedTreeHierarchy()
    {
        // Arrange
        var (token, companyId) = await SetupCompanyAsync();

        // 1. Create Parent Dept
        var parentReq = new CreateDepartmentRequest { Name = "Khối Công Nghệ", Code = "TECH-ROOT" };
        var msgP = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/departments")
        {
            Content = JsonContent.Create(parentReq)
        };
        msgP.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resP = await _client.SendAsync(msgP);
        var parent = await resP.Content.ReadFromJsonAsync<DepartmentResponse>();

        // 2. Create Child Dept
        var childReq = new CreateDepartmentRequest { Name = "Nhóm Backend", Code = "BE", ParentId = parent!.Id };
        var msgC = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/departments")
        {
            Content = JsonContent.Create(childReq)
        };
        msgC.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await _client.SendAsync(msgC);

        // Act - Fetch with ?tree=true
        var msgTree = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/departments?tree=true");
        msgTree.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(msgTree);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tree = await response.Content.ReadFromJsonAsync<List<DepartmentTreeNodeResponse>>();
        Assert.NotNull(tree);
        var techRoot = tree.FirstOrDefault(d => d.Id == parent.Id);
        Assert.NotNull(techRoot);
        Assert.Single(techRoot.Children);
        Assert.Equal("Nhóm Backend", techRoot.Children[0].Name);
    }

    [Fact]
    public async Task UpdateDepartment_SetParentAsSelf_Returns400BadRequest()
    {
        // Arrange
        var (token, companyId) = await SetupCompanyAsync();

        var request = new CreateDepartmentRequest { Name = "Phòng Độc Lập" };
        var msg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/departments")
        {
            Content = JsonContent.Create(request)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _client.SendAsync(msg);
        var dept = await res.Content.ReadFromJsonAsync<DepartmentResponse>();

        // Act - Try setting parent to itself
        var updateRequest = new UpdateDepartmentRequest { ParentId = dept!.Id };
        var msgUpdate = new HttpRequestMessage(HttpMethod.Patch, $"/departments/{dept.Id}")
        {
            Content = JsonContent.Create(updateRequest)
        };
        msgUpdate.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(msgUpdate);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RemoveDepartment_WithChildren_Returns400BadRequest()
    {
        // Arrange
        var (token, companyId) = await SetupCompanyAsync();

        // 1. Create Parent
        var pReq = new CreateDepartmentRequest { Name = "Phòng Cha" };
        var msgP = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/departments")
        {
            Content = JsonContent.Create(pReq)
        };
        msgP.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resP = await _client.SendAsync(msgP);
        var parent = await resP.Content.ReadFromJsonAsync<DepartmentResponse>();

        // 2. Create Child
        var cReq = new CreateDepartmentRequest { Name = "Phòng Con", ParentId = parent!.Id };
        var msgC = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/departments")
        {
            Content = JsonContent.Create(cReq)
        };
        msgC.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await _client.SendAsync(msgC);

        // Act - Try to delete parent
        var msgDel = new HttpRequestMessage(HttpMethod.Delete, $"/departments/{parent.Id}");
        msgDel.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(msgDel);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RemoveDepartment_LeafNode_Returns200OK()
    {
        // Arrange
        var (token, companyId) = await SetupCompanyAsync();

        var request = new CreateDepartmentRequest { Name = "Phòng Đơn Lẻ" };
        var msg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/departments")
        {
            Content = JsonContent.Create(request)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _client.SendAsync(msg);
        var dept = await res.Content.ReadFromJsonAsync<DepartmentResponse>();

        // Act
        var msgDel = new HttpRequestMessage(HttpMethod.Delete, $"/departments/{dept!.Id}");
        msgDel.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(msgDel);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify deleted
        var msgGet = new HttpRequestMessage(HttpMethod.Get, $"/departments/{dept.Id}");
        msgGet.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resGet = await _client.SendAsync(msgGet);
        Assert.Equal(HttpStatusCode.NotFound, resGet.StatusCode);
    }
}
