using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NexaOps.Application.Auth.DTOs;
using NexaOps.Application.Companies.DTOs;
using NexaOps.Application.CompanyMembers.DTOs;
using NexaOps.Application.Forms.DTOs;
using NexaOps.Domain.Enums;
using NexaOps.IntegrationTests.Common;

namespace NexaOps.IntegrationTests;

public class FormAnalyticsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public FormAnalyticsApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string Token, Guid CompanyId, Guid UserId)> SetupCompanyAndOwnerAsync()
    {
        var tag = Guid.NewGuid().ToString("N");
        var registerRequest = new RegisterRequest
        {
            Name = $"Analytics Owner {tag}",
            Email = $"analyticsowner.{tag}@nexaops.com",
            Password = "password123"
        };
        var regRes = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        var authResult = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        var token = authResult!.AccessToken;

        var compRequest = new CreateCompanyRequest { Name = $"Analytics Corp {tag}" };
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
        var email = $"member.{tag}@nexaops.com";
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
    public async Task GetCompanyOverview_ReturnsAccurateMetricsAndTrend()
    {
        // Arrange
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync();
        var (memberToken, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member);

        // 1. Create Form 1 (Draft)
        var createMsg1 = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms")
        {
            Content = JsonContent.Create(new CreateFormDto
            {
                Title = "Form Draft",
                Code = "DRAFT_" + Guid.NewGuid().ToString("N")[..6],
                Fields = new List<FormFieldDto>()
            })
        };
        createMsg1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        await _client.SendAsync(createMsg1);

        // 2. Create Form 2 and Publish
        var createMsg2 = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms")
        {
            Content = JsonContent.Create(new CreateFormDto
            {
                Title = "Form Request",
                Code = "REQ_" + Guid.NewGuid().ToString("N")[..6],
                Fields = new List<FormFieldDto>
                {
                    new() { Id = "note", Name = "note", Label = "Ghi chú", Type = FormFieldType.Text, Required = true }
                }
            })
        };
        createMsg2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var res2 = await _client.SendAsync(createMsg2);
        var form2 = await res2.Content.ReadFromJsonAsync<FormDefinitionDto>(JsonOptions);

        var pubMsg = new HttpRequestMessage(HttpMethod.Patch, $"/companies/{companyId}/forms/{form2!.Id}/status")
        {
            Content = JsonContent.Create(new ChangeFormStatusDto { Status = FormStatus.Published })
        };
        pubMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        await _client.SendAsync(pubMsg);

        // 3. Member submits 3 times
        for (int i = 1; i <= 3; i++)
        {
            var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form2.Id}/submissions")
            {
                Content = JsonContent.Create(new SubmitFormDto
                {
                    Data = new Dictionary<string, object?> { { "note", $"Entry {i}" } }
                })
            };
            submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
            var subRes = await _client.SendAsync(submitMsg);
            var subDto = await subRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

            // Approve submission 1 & 2, Reject submission 3
            if (i <= 2)
            {
                var reviewMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{subDto!.Id}/review")
                {
                    Content = JsonContent.Create(new ReviewSubmissionDto { Status = SubmissionStatus.Approved, Notes = "Approved" })
                };
                reviewMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
                await _client.SendAsync(reviewMsg);
            }
            else
            {
                var reviewMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{subDto!.Id}/review")
                {
                    Content = JsonContent.Create(new ReviewSubmissionDto { Status = SubmissionStatus.Rejected, Notes = "Rejected" })
                };
                reviewMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
                await _client.SendAsync(reviewMsg);
            }
        }

        // Act - Get Company Analytics Overview
        var analyticsMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/forms/analytics/overview?days=7");
        analyticsMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var analyticsRes = await _client.SendAsync(analyticsMsg);

        // Assert
        Assert.Equal(HttpStatusCode.OK, analyticsRes.StatusCode);
        var overview = await analyticsRes.Content.ReadFromJsonAsync<CompanyFormAnalyticsDto>(JsonOptions);
        Assert.NotNull(overview);
        Assert.Equal(2, overview.TotalForms);
        Assert.Equal(1, overview.DraftForms);
        Assert.Equal(1, overview.PublishedForms);
        Assert.Equal(3, overview.TotalSubmissions);
        Assert.Equal(2, overview.ApprovedSubmissions);
        Assert.Equal(1, overview.RejectedSubmissions);
        Assert.Equal(66.67, overview.ApprovalRatePercentage);
        Assert.Equal(7, overview.RecentDailySubmissions.Count);
        Assert.Single(overview.TopActiveForms);
        Assert.Equal(3, overview.TopActiveForms[0].SubmissionCount);
    }

    [Fact]
    public async Task GetFormAnalytics_CalculatesDetailsAndReviewTime()
    {
        // Arrange
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync();
        var (memberToken, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member);

        // Create & publish Form
        var createMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms")
        {
            Content = JsonContent.Create(new CreateFormDto
            {
                Title = "Review Time Form",
                Code = "REV_" + Guid.NewGuid().ToString("N")[..6],
                Fields = new List<FormFieldDto>
                {
                    new() { Id = "name", Name = "name", Type = FormFieldType.Text, Required = true }
                }
            })
        };
        createMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var res = await _client.SendAsync(createMsg);
        var form = await res.Content.ReadFromJsonAsync<FormDefinitionDto>(JsonOptions);

        var pubMsg = new HttpRequestMessage(HttpMethod.Patch, $"/companies/{companyId}/forms/{form!.Id}/status")
        {
            Content = JsonContent.Create(new ChangeFormStatusDto { Status = FormStatus.Published })
        };
        pubMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        await _client.SendAsync(pubMsg);

        // Submit & approve
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { { "name", "Test Item" } }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var subRes = await _client.SendAsync(submitMsg);
        var subDto = await subRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        var reviewMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{subDto!.Id}/review")
        {
            Content = JsonContent.Create(new ReviewSubmissionDto { Status = SubmissionStatus.Approved })
        };
        reviewMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        await _client.SendAsync(reviewMsg);

        // Act - Get Form Detail Analytics
        var formAnalyticsMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/forms/{form.Id}/analytics");
        formAnalyticsMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var formAnalyticsRes = await _client.SendAsync(formAnalyticsMsg);

        // Assert
        Assert.Equal(HttpStatusCode.OK, formAnalyticsRes.StatusCode);
        var details = await formAnalyticsRes.Content.ReadFromJsonAsync<FormDetailAnalyticsDto>(JsonOptions);
        Assert.NotNull(details);
        Assert.Equal(form.Id, details.FormId);
        Assert.Equal(1, details.TotalSubmissions);
        Assert.Equal(1, details.ApprovedCount);
        Assert.Equal(0, details.PendingCount);
        Assert.Equal(100.0, details.ApprovalRatePercentage);
        Assert.NotNull(details.AverageReviewTimeHours);
    }

    [Fact]
    public async Task Analytics_AccessControl_ManagerAllowed_MemberForbidden()
    {
        // Arrange
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync();
        var (managerToken, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager);
        var (memberToken, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member);

        // Act 1: Manager accesses overview -> 200 OK
        var mgrMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/forms/analytics/overview");
        mgrMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var mgrRes = await _client.SendAsync(mgrMsg);
        Assert.Equal(HttpStatusCode.OK, mgrRes.StatusCode);

        // Act 2: Member accesses overview -> 403 Forbidden
        var memMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyId}/forms/analytics/overview");
        memMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var memRes = await _client.SendAsync(memMsg);
        Assert.Equal(HttpStatusCode.Forbidden, memRes.StatusCode);
    }

    [Fact]
    public async Task Analytics_CrossTenantAccess_Returns403Forbidden()
    {
        // Company A & Company B
        var (tokenA, companyAId, _) = await SetupCompanyAndOwnerAsync();
        var (tokenB, companyBId, _) = await SetupCompanyAndOwnerAsync();

        // Owner B tries to access Company A analytics -> 403 Forbidden
        var crossMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyAId}/forms/analytics/overview");
        crossMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var response = await _client.SendAsync(crossMsg);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
