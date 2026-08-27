using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NexaOps.Application.Auth.DTOs;
using NexaOps.Application.Users.DTOs;
using NexaOps.IntegrationTests.Common;

namespace NexaOps.IntegrationTests;

public class AuthApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ValidPayload_Returns201AndValidToken()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Name = "Auth Test User",
            Email = $"auth.{Guid.NewGuid()}@nexaops.com",
            Password = "securePassword123"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/auth/register", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResponse);
        Assert.Equal("Đăng ký tài khoản thành công", authResponse.Message);
        Assert.False(string.IsNullOrWhiteSpace(authResponse.AccessToken));
        Assert.Equal(request.Name, authResponse.User.Name);
        Assert.Equal(request.Email.ToLowerInvariant(), authResponse.User.Email);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns400BadRequest()
    {
        // Arrange
        var email = $"dupauth.{Guid.NewGuid()}@nexaops.com";
        var request1 = new RegisterRequest
        {
            Name = "User One",
            Email = email,
            Password = "password123"
        };
        var request2 = new RegisterRequest
        {
            Name = "User Two",
            Email = email,
            Password = "password456"
        };

        // Act
        var response1 = await _client.PostAsJsonAsync("/auth/register", request1);
        Assert.Equal(HttpStatusCode.Created, response1.StatusCode);

        var response2 = await _client.PostAsJsonAsync("/auth/register", request2);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200AndToken()
    {
        // Arrange
        var email = $"login.{Guid.NewGuid()}@nexaops.com";
        var password = "mySecretPassword123";

        var registerRequest = new RegisterRequest
        {
            Name = "Login User",
            Email = email,
            Password = password
        };
        var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = password
        };

        // Act
        var loginResponse = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResult);
        Assert.Equal("Đăng nhập thành công", authResult.Message);
        Assert.False(string.IsNullOrWhiteSpace(authResult.AccessToken));
        Assert.Equal(email.ToLowerInvariant(), authResult.User.Email);
    }

    [Fact]
    public async Task Login_InvalidPassword_Returns401Unauthorized()
    {
        // Arrange
        var email = $"wrongpass.{Guid.NewGuid()}@nexaops.com";
        var registerRequest = new RegisterRequest
        {
            Name = "Wrong Pass User",
            Email = email,
            Password = "correctPassword123"
        };
        await _client.PostAsJsonAsync("/auth/register", registerRequest);

        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = "incorrectPassword!"
        };

        // Act
        var loginResponse = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithValidBearerToken_Returns200AndCurrentUser()
    {
        // Arrange
        var email = $"getme.{Guid.NewGuid()}@nexaops.com";
        var registerRequest = new RegisterRequest
        {
            Name = "Get Me User",
            Email = email,
            Password = "password123"
        };
        var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        var authData = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authData!.AccessToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(me);
        Assert.Equal(registerRequest.Name, me.Name);
        Assert.Equal(email.ToLowerInvariant(), me.Email);
    }

    [Fact]
    public async Task GetMe_WithoutToken_Returns401Unauthorized()
    {
        // Act
        var response = await _client.GetAsync("/auth/me");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
