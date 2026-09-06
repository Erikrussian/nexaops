using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NexaOps.Application.Auth.DTOs;
using NexaOps.Application.Companies.DTOs;
using NexaOps.Application.CompanyMembers.DTOs;
using NexaOps.Domain.Enums;
using NexaOps.IntegrationTests.Common;

namespace NexaOps.IntegrationTests;

public class CompanyMembersApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public CompanyMembersApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(string Token, string Email, Guid UserId)> RegisterUserAsync(string? prefix = null)
    {
        var tag = $"{prefix ?? "user"}_{Guid.NewGuid():N}";
        var email = $"member.{tag}@nexaops.com";
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

    private async Task<(string OwnerToken, string OwnerEmail, Guid CompanyId)> SetupCompanyAsync()
    {
        var (token, email, _) = await RegisterUserAsync("owner");
        var compRequest = new CreateCompanyRequest { Name = $"Member Co {Guid.NewGuid():N}" };
        var msg = new HttpRequestMessage(HttpMethod.Post, "/companies")
        {
            Content = JsonContent.Create(compRequest)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _client.SendAsync(msg);
        var company = await res.Content.ReadFromJsonAsync<CompanyResponse>();
        return (token, email, company!.Id);
    }

    [Fact]
    public async Task InviteMember_ValidPayload_Returns201CreatedAndInviteToken()
    {
        // Arrange
        var (ownerToken, _, companyId) = await SetupCompanyAsync();
        var inviteEmail = $"invitee.{Guid.NewGuid():N}@nexaops.com";

        var request = new InviteMemberRequest
        {
            Email = inviteEmail,
            Role = MemberRole.Manager
        };

        var msg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/members/invite")
        {
            Content = JsonContent.Create(request)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        // Act
        var response = await _client.SendAsync(msg);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var inviteResult = await response.Content.ReadFromJsonAsync<InviteMemberResponse>();
        Assert.NotNull(inviteResult);
        Assert.Equal(inviteEmail.ToLowerInvariant(), inviteResult.Member.Email);
        Assert.Equal("MANAGER", inviteResult.Member.Role);
        Assert.Equal("INVITED", inviteResult.Member.Status);
        Assert.False(string.IsNullOrWhiteSpace(inviteResult.InviteLink));
    }

    [Fact]
    public async Task AcceptInvite_ValidToken_Returns200AndActivatesMember()
    {
        // Arrange - Owner invites
        var (ownerToken, _, companyId) = await SetupCompanyAsync();
        var (inviteeToken, inviteeEmail, _) = await RegisterUserAsync("invitee");

        var inviteReq = new InviteMemberRequest { Email = inviteeEmail, Role = MemberRole.Member };
        var msgInvite = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/members/invite")
        {
            Content = JsonContent.Create(inviteReq)
        };
        msgInvite.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var inviteRes = await _client.SendAsync(msgInvite);
        var inviteData = await inviteRes.Content.ReadFromJsonAsync<InviteMemberResponse>();

        // Extract token from link
        var token = inviteData!.InviteLink.Split("token=")[1];

        // Act - Invitee accepts
        var acceptReq = new AcceptInviteRequest { InviteToken = token };
        var msgAccept = new HttpRequestMessage(HttpMethod.Post, "/company-members/accept-invite")
        {
            Content = JsonContent.Create(acceptReq)
        };
        msgAccept.Headers.Authorization = new AuthenticationHeaderValue("Bearer", inviteeToken);
        var acceptRes = await _client.SendAsync(msgAccept);

        // Assert
        Assert.Equal(HttpStatusCode.OK, acceptRes.StatusCode);
        var acceptResult = await acceptRes.Content.ReadFromJsonAsync<AcceptInviteResponse>();
        Assert.NotNull(acceptResult);
        Assert.Equal("Bạn đã gia nhập công ty thành công!", acceptResult.Message);
        Assert.Equal("MEMBER", acceptResult.Role);
    }

    [Fact]
    public async Task AcceptInvite_WrongUserEmail_Returns401Unauthorized()
    {
        // Arrange - Owner invites email A
        var (ownerToken, _, companyId) = await SetupCompanyAsync();
        var emailA = $"target.{Guid.NewGuid():N}@nexaops.com";
        var (wrongUserToken, _, _) = await RegisterUserAsync("wronguser");

        var inviteReq = new InviteMemberRequest { Email = emailA };
        var msgInvite = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/members/invite")
        {
            Content = JsonContent.Create(inviteReq)
        };
        msgInvite.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var inviteRes = await _client.SendAsync(msgInvite);
        var inviteData = await inviteRes.Content.ReadFromJsonAsync<InviteMemberResponse>();
        var token = inviteData!.InviteLink.Split("token=")[1];

        // Act - User B tries to accept
        var acceptReq = new AcceptInviteRequest { InviteToken = token };
        var msgAccept = new HttpRequestMessage(HttpMethod.Post, "/company-members/accept-invite")
        {
            Content = JsonContent.Create(acceptReq)
        };
        msgAccept.Headers.Authorization = new AuthenticationHeaderValue("Bearer", wrongUserToken);
        var acceptRes = await _client.SendAsync(msgAccept);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, acceptRes.StatusCode);
    }

    [Fact]
    public async Task GetMembersByCompany_ReturnsMemberList()
    {
        // Arrange
        var (ownerToken, _, companyId) = await SetupCompanyAsync();

        var msg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/members");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);

        // Act
        var response = await _client.SendAsync(msg);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<CompanyMemberResponse>>();
        Assert.NotNull(list);
    }

    [Fact]
    public async Task RemoveMember_OwnerRole_Returns400BadRequest()
    {
        // Arrange
        var (ownerToken, _, companyId) = await SetupCompanyAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexaOps.Infrastructure.Persistence.ApplicationDbContext>();
            var ownerMember = new NexaOps.Domain.Entities.CompanyMember
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Email = $"owner_{Guid.NewGuid():N}@nexaops.com",
                Role = MemberRole.Owner,
                Status = MemberStatus.Active
            };
            db.CompanyMembers.Add(ownerMember);
            await db.SaveChangesAsync();

            // Act - Try to remove OWNER
            var msgDel = new HttpRequestMessage(HttpMethod.Delete, $"/company-members/{ownerMember.Id}");
            msgDel.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
            var delRes = await _client.SendAsync(msgDel);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, delRes.StatusCode);
        }
    }

    [Fact]
    public async Task RemoveMember_RegularMember_Returns200OK()
    {
        // Arrange
        var (ownerToken, _, companyId) = await SetupCompanyAsync();

        var inviteEmail = $"regular.{Guid.NewGuid():N}@nexaops.com";
        var inviteReq = new InviteMemberRequest { Email = inviteEmail, Role = MemberRole.Member };
        var msgInvite = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/members/invite")
        {
            Content = JsonContent.Create(inviteReq)
        };
        msgInvite.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var inviteRes = await _client.SendAsync(msgInvite);
        var inviteData = await inviteRes.Content.ReadFromJsonAsync<InviteMemberResponse>();

        // Act - Remove regular member
        var msgDel = new HttpRequestMessage(HttpMethod.Delete, $"/company-members/{inviteData!.Member.Id}");
        msgDel.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var delRes = await _client.SendAsync(msgDel);

        // Assert
        Assert.Equal(HttpStatusCode.OK, delRes.StatusCode);
    }
}
