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

public class FormsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public FormsApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string Token, Guid CompanyId, Guid UserId)> SetupCompanyAndOwnerAsync()
    {
        var tag = Guid.NewGuid().ToString("N");
        var registerRequest = new RegisterRequest
        {
            Name = $"Form Owner {tag}",
            Email = $"formowner.{tag}@nexaops.com",
            Password = "password123"
        };
        var regRes = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        var authResult = await regRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        var token = authResult!.AccessToken;

        var compRequest = new CreateCompanyRequest { Name = $"Form Company {tag}" };
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
    public async Task CreateForm_AsOwner_Returns201Created()
    {
        // Arrange
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync();

        var request = new CreateFormDto
        {
            Title = "Đơn xin nghỉ phép",
            Code = "LEAVE_REQ_" + Guid.NewGuid().ToString("N")[..6],
            Description = "Form đăng ký nghỉ phép của nhân viên",
            Fields = new List<FormFieldDto>
            {
                new() { Id = "fullName", Name = "fullName", Label = "Họ và tên", Type = FormFieldType.Text, Required = true },
                new() { Id = "days", Name = "days", Label = "Số ngày nghỉ", Type = FormFieldType.Number, Required = true, Min = 1, Max = 30 },
                new() { Id = "reason", Name = "reason", Label = "Lý do nghỉ", Type = FormFieldType.Textarea, Required = true }
            }
        };

        // Act
        var msg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms")
        {
            Content = JsonContent.Create(request)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(msg);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var form = await response.Content.ReadFromJsonAsync<FormDefinitionDto>(JsonOptions);
        Assert.NotNull(form);
        Assert.Equal(request.Title, form.Title);
        Assert.Equal(FormStatus.Draft, form.Status);
        Assert.Equal(1, form.Version);
        Assert.Equal(3, form.Fields.Count);
    }

    [Fact]
    public async Task CreateForm_DuplicateCode_Returns400BadRequest()
    {
        // Arrange
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync();
        var code = "ONBOARDING_" + Guid.NewGuid().ToString("N")[..6];

        var request = new CreateFormDto
        {
            Title = "Form 1",
            Code = code,
            Fields = new List<FormFieldDto>()
        };

        var msg1 = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms")
        {
            Content = JsonContent.Create(request)
        };
        msg1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await _client.SendAsync(msg1);

        // Act - create second form with same code
        var msg2 = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms")
        {
            Content = JsonContent.Create(request)
        };
        msg2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response2 = await _client.SendAsync(msg2);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);
    }

    [Fact]
    public async Task FormLifecycle_UpdateAndPublish_Succeeds()
    {
        // Arrange
        var (token, companyId, _) = await SetupCompanyAndOwnerAsync();

        var createReq = new CreateFormDto
        {
            Title = "Đánh giá thử việc",
            Code = "PROBATION_" + Guid.NewGuid().ToString("N")[..6],
            Fields = new List<FormFieldDto>
            {
                new() { Id = "score", Name = "score", Label = "Điểm đánh giá", Type = FormFieldType.Number, Required = true }
            }
        };

        var createMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms")
        {
            Content = JsonContent.Create(createReq)
        };
        createMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createRes = await _client.SendAsync(createMsg);
        var form = await createRes.Content.ReadFromJsonAsync<FormDefinitionDto>(JsonOptions);

        // Act 1 - Update Form Fields (increments version)
        var updateReq = new UpdateFormDto
        {
            Title = "Đánh giá thử việc v2",
            Fields = new List<FormFieldDto>
            {
                new() { Id = "score", Name = "score", Label = "Điểm đánh giá", Type = FormFieldType.Number, Required = true, Min = 1, Max = 100 },
                new() { Id = "comment", Name = "comment", Label = "Nhận xét", Type = FormFieldType.Textarea }
            }
        };
        var updateMsg = new HttpRequestMessage(HttpMethod.Put, $"/companies/{companyId}/forms/{form!.Id}")
        {
            Content = JsonContent.Create(updateReq)
        };
        updateMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var updateRes = await _client.SendAsync(updateMsg);
        var updatedForm = await updateRes.Content.ReadFromJsonAsync<FormDefinitionDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        Assert.Equal(2, updatedForm!.Version);
        Assert.Equal(2, updatedForm.Fields.Count);

        // Act 2 - Change status to Published
        var statusMsg = new HttpRequestMessage(HttpMethod.Patch, $"/companies/{companyId}/forms/{form.Id}/status")
        {
            Content = JsonContent.Create(new ChangeFormStatusDto { Status = FormStatus.Published })
        };
        statusMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var statusRes = await _client.SendAsync(statusMsg);
        var publishedForm = await statusRes.Content.ReadFromJsonAsync<FormDefinitionDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, statusRes.StatusCode);
        Assert.Equal(FormStatus.Published, publishedForm!.Status);
    }

    [Fact]
    public async Task SubmitForm_DynamicValidation_HandlesValidAndInvalidPayloads()
    {
        // Arrange
        var (ownerToken, companyId, _) = await SetupCompanyAndOwnerAsync();
        var (memberToken, memberUserId) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member);

        // Create and Publish Form
        var createMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms")
        {
            Content = JsonContent.Create(new CreateFormDto
            {
                Title = "Phiếu khảo sát",
                Code = "SURVEY_" + Guid.NewGuid().ToString("N")[..6],
                Fields = new List<FormFieldDto>
                {
                    new() { Id = "email", Name = "email", Label = "Email liên hệ", Type = FormFieldType.Email, Required = true },
                    new() { Id = "age", Name = "age", Label = "Tuổi", Type = FormFieldType.Number, Required = true, Min = 18, Max = 65 },
                    new() { Id = "department", Name = "department", Label = "Phòng ban", Type = FormFieldType.Select, Required = true, Options = new() { "IT", "HR", "Sales" } }
                }
            })
        };
        createMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var createRes = await _client.SendAsync(createMsg);
        var form = await createRes.Content.ReadFromJsonAsync<FormDefinitionDto>(JsonOptions);

        // 1. Submit on DRAFT form -> 400 Bad Request
        var submitOnDraftMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form!.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { { "email", "test@test.com" }, { "age", 25 }, { "department", "IT" } }
            })
        };
        submitOnDraftMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var draftSubRes = await _client.SendAsync(submitOnDraftMsg);
        Assert.Equal(HttpStatusCode.BadRequest, draftSubRes.StatusCode);

        // Publish Form
        var pubMsg = new HttpRequestMessage(HttpMethod.Patch, $"/companies/{companyId}/forms/{form.Id}/status")
        {
            Content = JsonContent.Create(new ChangeFormStatusDto { Status = FormStatus.Published })
        };
        pubMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        await _client.SendAsync(pubMsg);

        // 2. Submit missing required email -> 400 Bad Request
        var missingFieldMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { { "age", 25 }, { "department", "IT" } }
            })
        };
        missingFieldMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var missingRes = await _client.SendAsync(missingFieldMsg);
        Assert.Equal(HttpStatusCode.BadRequest, missingRes.StatusCode);

        // 3. Submit invalid email format & age out of bounds & invalid select option -> 400 Bad Request
        var invalidDataMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { { "email", "not-an-email" }, { "age", 12 }, { "department", "Marketing" } }
            })
        };
        invalidDataMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var invalidRes = await _client.SendAsync(invalidDataMsg);
        Assert.Equal(HttpStatusCode.BadRequest, invalidRes.StatusCode);

        // 4. Submit perfectly valid payload -> 201 Created
        var validMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { { "email", "employee@company.com" }, { "age", 28 }, { "department", "IT" } }
            })
        };
        validMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var validRes = await _client.SendAsync(validMsg);
        Assert.Equal(HttpStatusCode.Created, validRes.StatusCode);
        var submission = await validRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.NotNull(submission);
        Assert.Equal(SubmissionStatus.Pending, submission.Status);
        Assert.Equal(memberUserId, submission.SubmittedById);
    }

    [Fact]
    public async Task ReviewSubmission_AsManagerOrOwner_Succeeds_And_MemberForbidden()
    {
        // Arrange
        var (ownerToken, companyId, ownerUserId) = await SetupCompanyAndOwnerAsync();
        var (managerToken, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Manager);
        var (memberToken, _) = await CreateUserAndJoinCompanyAsync(ownerToken, companyId, MemberRole.Member);

        // Create and Publish Form
        var createMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms")
        {
            Content = JsonContent.Create(new CreateFormDto
            {
                Title = "Thiết bị",
                Code = "DEVICE_" + Guid.NewGuid().ToString("N")[..6],
                Fields = new List<FormFieldDto>
                {
                    new() { Id = "device", Name = "device", Label = "Tên thiết bị", Type = FormFieldType.Text, Required = true }
                }
            })
        };
        createMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var createRes = await _client.SendAsync(createMsg);
        var form = await createRes.Content.ReadFromJsonAsync<FormDefinitionDto>(JsonOptions);

        var pubMsg = new HttpRequestMessage(HttpMethod.Patch, $"/companies/{companyId}/forms/{form!.Id}/status")
        {
            Content = JsonContent.Create(new ChangeFormStatusDto { Status = FormStatus.Published })
        };
        pubMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        await _client.SendAsync(pubMsg);

        // Member submits
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/forms/{form.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { { "device", "MacBook Pro M3" } }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var submitRes = await _client.SendAsync(submitMsg);
        var submission = await submitRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);

        // Act 1: Member tries to approve -> 403 Forbidden
        var memberReviewMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{submission!.Id}/review")
        {
            Content = JsonContent.Create(new ReviewSubmissionDto
            {
                Status = SubmissionStatus.Approved,
                Notes = "Self approval"
            })
        };
        memberReviewMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", memberToken);
        var memberReviewRes = await _client.SendAsync(memberReviewMsg);
        Assert.Equal(HttpStatusCode.Forbidden, memberReviewRes.StatusCode);

        // Act 2: Manager reviews and approves -> 200 OK
        var managerReviewMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyId}/submissions/{submission.Id}/review")
        {
            Content = JsonContent.Create(new ReviewSubmissionDto
            {
                Status = SubmissionStatus.Approved,
                Notes = "Đã duyệt cấp thiết bị"
            })
        };
        managerReviewMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var managerReviewRes = await _client.SendAsync(managerReviewMsg);
        Assert.Equal(HttpStatusCode.OK, managerReviewRes.StatusCode);

        var reviewedSubmission = await managerReviewRes.Content.ReadFromJsonAsync<FormSubmissionDto>(JsonOptions);
        Assert.Equal(SubmissionStatus.Approved, reviewedSubmission!.Status);
        Assert.Equal("Đã duyệt cấp thiết bị", reviewedSubmission.ReviewNotes);
        Assert.NotNull(reviewedSubmission.ReviewedAt);
    }

    [Fact]
    public async Task TenantIsolation_CannotAccessOrSubmitCrossTenantForms()
    {
        // Company A
        var (tokenA, companyAId, _) = await SetupCompanyAndOwnerAsync();
        // Company B
        var (tokenB, companyBId, _) = await SetupCompanyAndOwnerAsync();

        // Create and Publish Form in Company A
        var createMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyAId}/forms")
        {
            Content = JsonContent.Create(new CreateFormDto
            {
                Title = "Form Tenant A",
                Code = "SECRET_A_" + Guid.NewGuid().ToString("N")[..6],
                Fields = new List<FormFieldDto> { new() { Id = "info", Name = "info", Type = FormFieldType.Text, Required = true } }
            })
        };
        createMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var createRes = await _client.SendAsync(createMsg);
        var formA = await createRes.Content.ReadFromJsonAsync<FormDefinitionDto>(JsonOptions);

        var pubMsg = new HttpRequestMessage(HttpMethod.Patch, $"/companies/{companyAId}/forms/{formA!.Id}/status")
        {
            Content = JsonContent.Create(new ChangeFormStatusDto { Status = FormStatus.Published })
        };
        pubMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        await _client.SendAsync(pubMsg);

        // User B tries to read Form A in Company A endpoint -> 403 Forbidden
        var readMsg = new HttpRequestMessage(HttpMethod.Get, $"/companies/{companyAId}/forms/{formA.Id}");
        readMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var readRes = await _client.SendAsync(readMsg);
        Assert.Equal(HttpStatusCode.Forbidden, readRes.StatusCode);

        // User B tries to submit to Form A using Company B context or Company A context -> 403 Forbidden / 404 Not Found
        var submitMsg = new HttpRequestMessage(HttpMethod.Post, $"/companies/{companyAId}/forms/{formA.Id}/submissions")
        {
            Content = JsonContent.Create(new SubmitFormDto
            {
                Data = new Dictionary<string, object?> { { "info", "tampering" } }
            })
        };
        submitMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var submitRes = await _client.SendAsync(submitMsg);
        Assert.Equal(HttpStatusCode.Forbidden, submitRes.StatusCode);
    }
}
