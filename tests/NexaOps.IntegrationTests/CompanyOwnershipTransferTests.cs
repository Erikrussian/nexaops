using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NexaOps.Application.Auth.DTOs;
using NexaOps.Application.Companies.DTOs;
using NexaOps.Application.CompanyMembers.DTOs;
using NexaOps.Domain.Enums;
using NexaOps.IntegrationTests.Common;

namespace NexaOps.IntegrationTests;

public class CompanyOwnershipTransferTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CompanyOwnershipTransferTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string Token, string Password, Guid CompanyId, Guid UserId)> SetupCompanyAndOwnerAsync()
    {
        var tag = Guid.NewGuid().ToString("N");
        var password = "StrongPassword123!";
        var registerRequest = new RegisterRequest
        {
            Name = $"Original Owner {tag}",
            Email = $"owner.{tag}@nexaops.com",
            Password = password
        };
        var regRes = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        var authResult = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        var token = authResult!.AccessToken;

        var compRequest = new CreateCompanyRequest { Name = $"Transfer Corp {tag}" };
        var msgComp = new HttpRequestMessage(HttpMethod.Post, "/companies")
        {
            Content = JsonContent.Create(compRequest)
        };
        msgComp.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var compRes = await _client.SendAsync(msgComp);
        var company = await compRes.Content.ReadFromJsonAsync<CompanyResponse>(JsonOptions);

        return (token, password, company!.Id, authResult.User.Id);
    }

    private async Task<(string Token, string Password, Guid UserId)> CreateUserAndJoinCompanyAsync(string ownerToken, Guid companyId, MemberRole role)
    {
        var tag = Guid.NewGuid().ToString("N");
        var password = "UserPassword123!";
        var email = $"member.{tag}@nexaops.com";
        var regRes = await _client.PostAsJsonAsync("/auth/register", new RegisterRequest
        {
            Name = $"Member {tag}",
            Email = email,
            Password = password
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

        return (memberToken, password, memberUserId);
    }

    [Fact]
    public async Task TransferOwnership_ValidPasswordAndActiveMember_TransfersSuccessfully()
    {
        // Arrange
        var (oldOwnerToken, oldOwnerPassword, companyId, oldOwnerId) = await SetupCompanyAndOwnerAsync();
        var (newOwnerToken, newOwnerPassword, newOwnerId) = await CreateUserAndJoinCompanyAsync(oldOwnerToken, companyId, MemberRole.Member);

        var transferRequest = new TransferOwnershipRequest
        {
            NewOwnerId = newOwnerId,
            Password = oldOwnerPassword,
            PreviousOwnerNewRole = MemberRole.Admin
        };

        // Act - Old owner transfers to new owner
        var msg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/transfer-ownership")
        {
            Content = JsonContent.Create(transferRequest)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", oldOwnerToken);
        var response = await _client.SendAsync(msg);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var company = await response.Content.ReadFromJsonAsync<CompanyResponse>(JsonOptions);
        Assert.NotNull(company);
        Assert.Equal(newOwnerId, company.OwnerId);

        // Verification 1: Old owner is no longer owner -> cannot perform owner-only operations (e.g. transfer ownership again)
        var oldOwnerTransferMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/transfer-ownership")
        {
            Content = JsonContent.Create(new TransferOwnershipRequest
            {
                NewOwnerId = oldOwnerId,
                Password = oldOwnerPassword
            })
        };
        oldOwnerTransferMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", oldOwnerToken);
        var forbiddenRes = await _client.SendAsync(oldOwnerTransferMsg);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRes.StatusCode);

        // Verification 2: New owner can now transfer ownership back
        var newOwnerTransferMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/transfer-ownership")
        {
            Content = JsonContent.Create(new TransferOwnershipRequest
            {
                NewOwnerId = oldOwnerId,
                Password = newOwnerPassword,
                PreviousOwnerNewRole = MemberRole.Manager
            })
        };
        newOwnerTransferMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newOwnerToken);
        var transferBackRes = await _client.SendAsync(newOwnerTransferMsg);
        Assert.Equal(HttpStatusCode.OK, transferBackRes.StatusCode);
        var transferredBackCompany = await transferBackRes.Content.ReadFromJsonAsync<CompanyResponse>(JsonOptions);
        Assert.Equal(oldOwnerId, transferredBackCompany!.OwnerId);
    }

    [Fact]
    public async Task TransferOwnership_WrongPassword_Returns400BadRequest()
    {
        // Arrange
        var (ownerToken, _, companyId, _) = await SetupCompanyAndOwnerAsync();
        var (_, _, newOwnerId) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member);

        var transferRequest = new TransferOwnershipRequest
        {
            NewOwnerId = newOwnerId,
            Password = "WrongPassword123!"
        };

        // Act
        var msg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/transfer-ownership")
        {
            Content = JsonContent.Create(transferRequest)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var response = await _client.SendAsync(msg);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TransferOwnership_AsNonOwner_Returns403Forbidden()
    {
        // Arrange
        var (ownerToken, _, companyId, _) = await SetupCompanyAndOwnerAsync();
        var (adminToken, adminPassword, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Admin);
        var (_, _, memberUserId) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member);

        // Act - Admin tries to transfer company ownership
        var msg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/transfer-ownership")
        {
            Content = JsonContent.Create(new TransferOwnershipRequest
            {
                NewOwnerId = memberUserId,
                Password = adminPassword
            })
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await _client.SendAsync(msg);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TransferOwnership_ToSelfOrNonMember_Returns400BadRequest()
    {
        // Arrange
        var (ownerToken, password, companyId, ownerId) = await SetupCompanyAndOwnerAsync();

        // 1. Transfer to self -> 400 Bad Request
        var selfMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/transfer-ownership")
        {
            Content = JsonContent.Create(new TransferOwnershipRequest
            {
                NewOwnerId = ownerId,
                Password = password
            })
        };
        selfMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var selfRes = await _client.SendAsync(selfMsg);
        Assert.Equal(HttpStatusCode.BadRequest, selfRes.StatusCode);

        // 2. Transfer to non-member user ID -> 400 Bad Request
        var nonMemberMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/transfer-ownership")
        {
            Content = JsonContent.Create(new TransferOwnershipRequest
            {
                NewOwnerId = Guid.NewGuid(),
                Password = password
            })
        };
        nonMemberMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var nonMemberRes = await _client.SendAsync(nonMemberMsg);
        Assert.Equal(HttpStatusCode.BadRequest, nonMemberRes.StatusCode);
    }

    [Fact]
    public async Task TransferOwnership_CrossTenant_Returns403Forbidden()
    {
        // Company A
        var (tokenA, passwordA, companyAId, _) = await SetupCompanyAndOwnerAsync();
        // Company B
        var (tokenB, _, companyBId, userBId) = await SetupCompanyAndOwnerAsync();

        // Owner of A tries to transfer Company B
        var msg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyBId}/transfer-ownership")
        {
            Content = JsonContent.Create(new TransferOwnershipRequest
            {
                NewOwnerId = userBId,
                Password = passwordA
            })
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var response = await _client.SendAsync(msg);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
