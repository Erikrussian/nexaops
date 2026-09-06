using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NexaOps.Application.Auth.DTOs;
using NexaOps.Application.Companies.DTOs;
using NexaOps.Application.CompanyMembers.DTOs;
using NexaOps.Application.Departments.DTOs;
using NexaOps.Domain.Enums;
using NexaOps.IntegrationTests.Common;

namespace NexaOps.IntegrationTests;

public class TenantIsolationApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TenantIsolationApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string Token, string Email, Guid UserId)> RegisterUserAsync(string prefix, UserRole role = UserRole.User)
    {
        var tag = $"{prefix}_{Guid.NewGuid():N}";
        var email = $"{tag}@nexaops.com";
        var regRequest = new RegisterRequest
        {
            Name = $"User {tag}",
            Email = email,
            Password = "password123"
        };
        var res = await _client.PostAsJsonAsync("/auth/register", regRequest);
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
        return (auth!.AccessToken, email, auth.User.Id);
    }

    private async Task<(string OwnerToken, Guid OwnerId, CompanyResponse Company)> SetupCompanyAsync(string prefix)
    {
        var (token, _, userId) = await RegisterUserAsync(prefix);
        var compRequest = new CreateCompanyRequest { Name = $"Co {prefix} {Guid.NewGuid():N}" };
        var msg = new HttpRequestMessage(HttpMethod.Post, "/companies")
        {
            Content = JsonContent.Create(compRequest)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _client.SendAsync(msg);
        var company = await res.Content.ReadFromJsonAsync<CompanyResponse>();
        return (token, userId, company!);
    }

    private async Task<(string MemberToken, Guid MemberId)> AddActiveMemberAsync(string ownerToken, Guid companyId, string memberPrefix, MemberRole role)
    {
        var (memToken, memEmail, _) = await RegisterUserAsync(memberPrefix);

        // Owner invites member
        var inviteReq = new InviteMemberRequest { Email = memEmail, Role = role };
        var msgInvite = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/members/invite")
        {
            Content = JsonContent.Create(inviteReq)
        };
        msgInvite.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var inviteRes = await _client.SendAsync(msgInvite);
        var inviteData = await inviteRes.Content.ReadFromJsonAsync<InviteMemberResponse>();
        var token = inviteData!.InviteLink.Split("token=")[1];

        // Member accepts invite
        var acceptReq = new AcceptInviteRequest { InviteToken = token };
        var msgAccept = new HttpRequestMessage(HttpMethod.Post, "/company-members/accept-invite")
        {
            Content = JsonContent.Create(acceptReq)
        };
        msgAccept.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memToken);
        await _client.SendAsync(msgAccept);

        return (memToken, inviteData.Member.Id);
    }

    [Fact]
    public async Task Requirement1_UnauthenticatedRequests_Return401Unauthorized()
    {
        // Act & Assert
        var resComp = await _client.GetAsync("/companies");
        Assert.Equal(HttpStatusCode.Unauthorized, resComp.StatusCode);

        var resDepts = await _client.GetAsync($"/companies/{Guid.NewGuid()}/departments");
        Assert.Equal(HttpStatusCode.Unauthorized, resDepts.StatusCode);

        var resMembers = await _client.GetAsync($"/companies/{Guid.NewGuid()}/members");
        Assert.Equal(HttpStatusCode.Unauthorized, resMembers.StatusCode);

        var resUsers = await _client.GetAsync("/users");
        Assert.Equal(HttpStatusCode.Unauthorized, resUsers.StatusCode);
    }

    [Fact]
    public async Task Requirement2_UserACannotViewCompanyBByIdOrSlug()
    {
        // Arrange
        var (tokenA, _, _) = await RegisterUserAsync("userA");
        var (_, _, companyB) = await SetupCompanyAsync("coB");

        // Act - By ID
        var msgId = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyB.Id}");
        msgId.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var resId = await _client.SendAsync(msgId);

        // Act - By Slug
        var msgSlug = new HttpRequestMessage(HttpMethod.Get, $"/companies/slug/{companyB.Slug}");
        msgSlug.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var resSlug = await _client.SendAsync(msgSlug);

        // Assert - Should be 403 Forbidden or 404 NotFound (not 200 OK)
        Assert.True(resId.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Expected 403 or 404 but got {resId.StatusCode}");
        Assert.True(resSlug.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Expected 403 or 404 but got {resSlug.StatusCode}");
    }

    [Fact]
    public async Task Requirement3_UserACannotViewDepartmentsOfCompanyB()
    {
        // Arrange
        var (tokenA, _, _) = await RegisterUserAsync("userA");
        var (tokenB, _, companyB) = await SetupCompanyAsync("coB");

        // Create department in Company B
        var createDeptReq = new CreateDepartmentRequest { Name = "Dept B1" };
        var msgCreate = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyB.Id}/departments")
        {
            Content = JsonContent.Create(createDeptReq)
        };
        msgCreate.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        await _client.SendAsync(msgCreate);

        // Act - User A tries to get departments of Company B
        var msgGet = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyB.Id}/departments");
        msgGet.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var response = await _client.SendAsync(msgGet);

        // Assert
        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Expected 403 or 404 but got {response.StatusCode}");
    }

    [Fact]
    public async Task Requirement4_UserACannotCreateDepartmentInCompanyB()
    {
        // Arrange
        var (tokenA, _, _) = await RegisterUserAsync("userA");
        var (_, _, companyB) = await SetupCompanyAsync("coB");

        var createDeptReq = new CreateDepartmentRequest { Name = "Hacked Dept" };
        var msg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyB.Id}/departments")
        {
            Content = JsonContent.Create(createDeptReq)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        // Act
        var response = await _client.SendAsync(msg);

        // Assert
        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Expected 403 or 404 but got {response.StatusCode}");
    }

    [Fact]
    public async Task Requirement5_UserACannotViewDepartmentOfCompanyBByDepartmentId()
    {
        // Arrange
        var (tokenA, _, _) = await RegisterUserAsync("userA");
        var (tokenB, _, companyB) = await SetupCompanyAsync("coB");

        var createDeptReq = new CreateDepartmentRequest { Name = "Secret Dept" };
        var msgCreate = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyB.Id}/departments")
        {
            Content = JsonContent.Create(createDeptReq)
        };
        msgCreate.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var createRes = await _client.SendAsync(msgCreate);
        var dept = await createRes.Content.ReadFromJsonAsync<DepartmentResponse>();

        // Act - User A requests by departmentId
        var msgGet = new HttpRequestMessage(HttpMethod.Get, $"/departments/{dept!.Id}");
        msgGet.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var response = await _client.SendAsync(msgGet);

        // Assert
        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Expected 403 or 404 but got {response.StatusCode}");
    }

    [Fact]
    public async Task Requirement6_UserACannotUpdateDepartmentOfCompanyB()
    {
        // Arrange
        var (tokenA, _, _) = await RegisterUserAsync("userA");
        var (tokenB, _, companyB) = await SetupCompanyAsync("coB");

        var createDeptReq = new CreateDepartmentRequest { Name = "Dept Original" };
        var msgCreate = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyB.Id}/departments")
        {
            Content = JsonContent.Create(createDeptReq)
        };
        msgCreate.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var createRes = await _client.SendAsync(msgCreate);
        var dept = await createRes.Content.ReadFromJsonAsync<DepartmentResponse>();

        // Act - User A attempts update
        var updateReq = new UpdateDepartmentRequest { Name = "Hacked Dept Name" };
        var msgUpdate = new HttpRequestMessage(HttpMethod.Patch, $"/departments/{dept!.Id}")
        {
            Content = JsonContent.Create(updateReq)
        };
        msgUpdate.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var response = await _client.SendAsync(msgUpdate);

        // Assert
        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Expected 403 or 404 but got {response.StatusCode}");
    }

    [Fact]
    public async Task Requirement7_UserACannotDeleteDepartmentOfCompanyB()
    {
        // Arrange
        var (tokenA, _, _) = await RegisterUserAsync("userA");
        var (tokenB, _, companyB) = await SetupCompanyAsync("coB");

        var createDeptReq = new CreateDepartmentRequest { Name = "Dept To Delete" };
        var msgCreate = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyB.Id}/departments")
        {
            Content = JsonContent.Create(createDeptReq)
        };
        msgCreate.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var createRes = await _client.SendAsync(msgCreate);
        var dept = await createRes.Content.ReadFromJsonAsync<DepartmentResponse>();

        // Act - User A attempts delete
        var msgDel = new HttpRequestMessage(HttpMethod.Delete, $"/departments/{dept!.Id}");
        msgDel.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var response = await _client.SendAsync(msgDel);

        // Assert
        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Expected 403 or 404 but got {response.StatusCode}");
    }

    [Fact]
    public async Task Requirement8_UserACannotViewMembersOfCompanyB()
    {
        // Arrange
        var (tokenA, _, _) = await RegisterUserAsync("userA");
        var (_, _, companyB) = await SetupCompanyAsync("coB");

        // Act
        var msg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyB.Id}/members");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var response = await _client.SendAsync(msg);

        // Assert
        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Expected 403 or 404 but got {response.StatusCode}");
    }

    [Fact]
    public async Task Requirement9_UserACannotInviteMembersIntoCompanyB()
    {
        // Arrange
        var (tokenA, _, _) = await RegisterUserAsync("userA");
        var (_, _, companyB) = await SetupCompanyAsync("coB");

        var inviteReq = new InviteMemberRequest { Email = "stranger@nexaops.com", Role = MemberRole.Member };
        var msg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyB.Id}/members/invite")
        {
            Content = JsonContent.Create(inviteReq)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        // Act
        var response = await _client.SendAsync(msg);

        // Assert
        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Expected 403 or 404 but got {response.StatusCode}");
    }

    [Fact]
    public async Task Requirement10_UserACannotUpdateMemberOfCompanyB()
    {
        // Arrange
        var (tokenA, _, _) = await RegisterUserAsync("userA");
        var (tokenB, _, companyB) = await SetupCompanyAsync("coB");
        var (_, memberId) = await AddActiveMemberAsync(tokenB, companyB.Id, "memberB", MemberRole.Member);

        // Act - User A attempts to update member B
        var updateReq = new UpdateMemberRequest { Role = MemberRole.Admin };
        var msg = new HttpRequestMessage(HttpMethod.Patch, $"/company-members/{memberId}")
        {
            Content = JsonContent.Create(updateReq)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var response = await _client.SendAsync(msg);

        // Assert
        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Expected 403 or 404 but got {response.StatusCode}");
    }

    [Fact]
    public async Task Requirement11_UserACannotDeleteMemberOfCompanyB()
    {
        // Arrange
        var (tokenA, _, _) = await RegisterUserAsync("userA");
        var (tokenB, _, companyB) = await SetupCompanyAsync("coB");
        var (_, memberId) = await AddActiveMemberAsync(tokenB, companyB.Id, "memberB", MemberRole.Member);

        // Act - User A attempts to delete member B
        var msg = new HttpRequestMessage(HttpMethod.Delete, $"/company-members/{memberId}");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var response = await _client.SendAsync(msg);

        // Assert
        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Expected 403 or 404 but got {response.StatusCode}");
    }

    [Fact]
    public async Task Requirement12_RegularMemberCannotPerformAdminOrOwnerOperations()
    {
        // Arrange
        var (ownerToken, _, company) = await SetupCompanyAsync("coReg");
        var (memberToken, _) = await AddActiveMemberAsync(ownerToken, company.Id, "regMem", MemberRole.Member);

        // Act 1: Regular Member tries to invite
        var inviteReq = new InviteMemberRequest { Email = "new@nexaops.com" };
        var msgInvite = new HttpRequestMessage(HttpMethod.Post, $"/companies/{company.Id}/members/invite")
        {
            Content = JsonContent.Create(inviteReq)
        };
        msgInvite.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var resInvite = await _client.SendAsync(msgInvite);

        // Act 2: Regular Member tries to create department
        var deptReq = new CreateDepartmentRequest { Name = "Forbidden Dept" };
        var msgDept = new HttpRequestMessage(HttpMethod.Post, $"/companies/{company.Id}/departments")
        {
            Content = JsonContent.Create(deptReq)
        };
        msgDept.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var resDept = await _client.SendAsync(msgDept);

        // Act 3: Regular Member tries to delete company
        var msgDelComp = new HttpRequestMessage(HttpMethod.Delete, $"/companies/{company.Id}");
        msgDelComp.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var resDelComp = await _client.SendAsync(msgDelComp);

        // Assert
        Assert.True(resInvite.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
            $"Expected 403 or 401 for invite but got {resInvite.StatusCode}");
        Assert.True(resDept.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
            $"Expected 403 or 401 for dept create but got {resDept.StatusCode}");
        Assert.True(resDelComp.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
            $"Expected 403 or 401 for comp delete but got {resDelComp.StatusCode}");
    }

    [Fact]
    public async Task Requirement13_AdminCannotAssignRoleOwner()
    {
        // Arrange
        var (ownerToken, _, company) = await SetupCompanyAsync("coAdmin");
        var (adminToken, _) = await AddActiveMemberAsync(ownerToken, company.Id, "adminUser", MemberRole.Admin);

        // Act 1: Admin tries to invite with role Owner
        var inviteReq = new InviteMemberRequest { Email = "newowner@nexaops.com", Role = MemberRole.Owner };
        var msgInvite = new HttpRequestMessage(HttpMethod.Post, $"/companies/{company.Id}/members/invite")
        {
            Content = JsonContent.Create(inviteReq)
        };
        msgInvite.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var resInvite = await _client.SendAsync(msgInvite);

        // Assert
        Assert.True(resInvite.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden,
            $"Expected 400 or 403 for assigning Owner role but got {resInvite.StatusCode}");
    }

    [Fact]
    public async Task Requirement15_ActiveMemberSeesJoinedCompanyInMyCompanies()
    {
        // Arrange
        var (ownerToken, _, company) = await SetupCompanyAsync("coJoined");
        var (memberToken, _) = await AddActiveMemberAsync(ownerToken, company.Id, "memJoined", MemberRole.Member);

        // Act
        var msg = new HttpRequestMessage(HttpMethod.Get, "/companies");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var response = await _client.SendAsync(msg);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<CompanyResponse>>();
        Assert.NotNull(list);
        Assert.Contains(list, c => c.Id == company.Id);
    }
}
