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
using NexaOps.Domain.Enums;
using NexaOps.IntegrationTests.Common;

namespace NexaOps.IntegrationTests;

public class AuditLogsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AuditLogsApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string Token, Guid CompanyId, Guid UserId)> SetupCompanyAndOwnerAsync()
    {
        var tag = Guid.NewGuid().ToString("N");
        var registerRequest = new RegisterRequest
        {
            Name = $"Audit Owner {tag}",
            Email = $"auditowner.{tag}@nexaops.com",
            Password = "password123"
        };
        var regRes = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        var authResult = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        var token = authResult!.AccessToken;

        var compRequest = new CreateCompanyRequest { Name = $"Audit Company {tag}" };
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
        var email = $"auditmember.{tag}@nexaops.com";
        var regRes = await _client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Name = $"Member {tag}",
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

    [Fact]
    public async Task AuditLogs_RecordedAutomatically_OnCompanyOperations()
    {
        // Arrange
        var (ownerToken, companyId, ownerUserId) = await SetupCompanyAndOwnerAsync();

        // Perform an update on Company
        var updateReq = new UpdateCompanyRequest { Description = "Updated description for audit" };
        var updateMsg = new HttpRequestMessage(HttpMethod.Patch, $"/companies/{companyId}")
        {
            Content = JsonContent.Create(updateReq)
        };
        updateMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        await _client.SendAsync(updateMsg);

        // Act - Query audit logs as Owner
        var logsMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/audit-logs");
        logsMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var response = await _client.SendAsync(logsMsg);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pagedResult = await response.Content.ReadFromJsonAsync<PagedResult<AuditLogDto>>(JsonOptions);
        Assert.NotNull(pagedResult);
        Assert.True(pagedResult.TotalCount >= 2); // CREATE Company + UPDATE Company

        var createLog = pagedResult.Items.FirstOrDefault(l => l.Action == "CREATE" && l.EntityType == "Company");
        Assert.NotNull(createLog);
        Assert.Equal(ownerUserId, createLog.ActorId);

        var updateLog = pagedResult.Items.FirstOrDefault(l => l.Action == "UPDATE" && l.EntityType == "Company");
        Assert.NotNull(updateLog);
        Assert.Equal(ownerUserId, updateLog.ActorId);
    }

    [Fact]
    public async Task AuditLogs_AccessibleByAdmin_AndForbiddenForRegularMember()
    {
        // Arrange
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync();
        var (adminToken, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Admin);
        var (memberToken, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member);

        // Act 1: Admin queries audit logs -> 200 OK
        var adminMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/audit-logs");
        adminMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var adminRes = await _client.SendAsync(adminMsg);
        Assert.Equal(HttpStatusCode.OK, adminRes.StatusCode);

        // Act 2: Regular member queries audit logs -> 403 Forbidden
        var memberMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/audit-logs");
        memberMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var memberRes = await _client.SendAsync(memberMsg);
        Assert.Equal(HttpStatusCode.Forbidden, memberRes.StatusCode);
    }

    [Fact]
    public async Task AuditLogs_FilterByActionAndPagination_ReturnsAccurateResults()
    {
        // Arrange
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync();

        // Update company multiple times
        for (int i = 1; i <= 3; i++)
        {
            var updateMsg = new HttpRequestMessage(HttpMethod.Patch, $"/companies/{companyId}")
            {
                Content = JsonContent.Create(new UpdateCompanyRequest { Description = $"Desc {i}" })
            };
            updateMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
            await _client.SendAsync(updateMsg);
        }

        // Act 1: Filter by Action=UPDATE
        var filterMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/audit-logs?action=UPDATE");
        filterMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var filterRes = await _client.SendAsync(filterMsg);
        var filterResult = await filterRes.Content.ReadFromJsonAsync<PagedResult<AuditLogDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, filterRes.StatusCode);
        Assert.NotNull(filterResult);
        Assert.All(filterResult.Items, item => Assert.Equal("UPDATE", item.Action));

        // Act 2: Pagination with pageSize=2
        var pagedMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/audit-logs?page=1&pageSize=2");
        pagedMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var pagedRes = await _client.SendAsync(pagedMsg);
        var pagedData = await pagedRes.Content.ReadFromJsonAsync<PagedResult<AuditLogDto>>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, pagedRes.StatusCode);
        Assert.NotNull(pagedData);
        Assert.Equal(2, pagedData.Items.Count);
        Assert.True(pagedData.TotalPages >= 2);
    }

    [Fact]
    public async Task AuditLogs_CrossTenantAccess_Returns403Forbidden()
    {
        // Arrange
        var (tokenA, companyAId, _) = await SetupCompanyAndOwnerAsync();
        var (tokenB, companyBId, _) = await SetupCompanyAndOwnerAsync();

        // Act - Owner of B tries to read Audit Logs of Company A
        var msg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyAId}/audit-logs");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var response = await _client.SendAsync(msg);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
