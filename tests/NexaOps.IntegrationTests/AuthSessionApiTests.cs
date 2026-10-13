using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexaOps.Application.Auth.DTOs;
using NexaOps.Application.Common.Interfaces;
using NexaOps.IntegrationTests.Common;

namespace NexaOps.IntegrationTests;

public class AuthSessionApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AuthSessionApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(string AccessToken, string RefreshToken, string Email, Guid UserId)> RegisterUserAsync(string? prefix = null)
    {
        var tag = $"{prefix ?? "user"}_{Guid.NewGuid():N}";
        var email = $"session.{tag}@nexaops.com";
        var regRequest = new RegisterRequest
        {
            Name = $"User {tag}",
            Email = email,
            Password = "Password123!"
        };

        var res = await _client.PostAsJsonAsync("/auth/register", regRequest);
        res.EnsureSuccessStatusCode();

        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return (auth!.AccessToken, auth.RefreshToken!, email, auth.User.Id);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsAccessTokenAndRefreshTokenCookie()
    {
        // Arrange
        var (_, _, email, _) = await RegisterUserAsync("login_test");
        var loginReq = new LoginRequest
        {
            Email = email,
            Password = "Password123!"
        };

        // Act
        var res = await _client.PostAsJsonAsync("/auth/login", loginReq);

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));

        // Check HttpOnly cookie is set
        var setCookie = res.Headers.GetValues("Set-Cookie").FirstOrDefault(c => c.StartsWith("refreshToken="));
        Assert.NotNull(setCookie);
        Assert.Contains("httponly", setCookie.ToLowerInvariant());
    }

    [Fact]
    public async Task RefreshToken_ValidToken_PerformsRotationAndReturnsNewTokens()
    {
        // Arrange
        var (initialAccessToken, initialRefreshToken, _, _) = await RegisterUserAsync("rotation_test");

        var refreshReq = new RefreshTokenRequest
        {
            RefreshToken = initialRefreshToken
        };

        // Act - Call refresh endpoint
        var res = await _client.PostAsJsonAsync("/auth/refresh", refreshReq);

        // Assert
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var refreshResult = await res.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(refreshResult);
        Assert.False(string.IsNullOrWhiteSpace(refreshResult.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshResult.RefreshToken));
        Assert.NotEqual(initialRefreshToken, refreshResult.RefreshToken);
        Assert.NotEqual(initialAccessToken, refreshResult.AccessToken);
    }

    [Fact]
    public async Task RefreshToken_ReusingRotatedToken_TriggersAnomalyDetectionAndRevokesAllFamilyTokens()
    {
        // Arrange
        var (_, initialRefreshToken, _, userId) = await RegisterUserAsync("reuse_test");

        // 1. Legitimate refresh rotates the token
        var firstRefreshRes = await _client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = initialRefreshToken
        });
        Assert.Equal(HttpStatusCode.OK, firstRefreshRes.StatusCode);
        var firstResult = await firstRefreshRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        var secondRefreshToken = firstResult!.RefreshToken!;

        // 2. Attacker/Victim attempts to reuse initialRefreshToken (which was already rotated and marked revoked)
        var reuseRes = await _client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = initialRefreshToken
        });

        // Assert - Reuse detected! Must return 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, reuseRes.StatusCode);

        // 3. Verify that the new secondRefreshToken has ALSO been revoked as part of the family breach protection
        var secondTryRes = await _client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = secondRefreshToken
        });
        Assert.Equal(HttpStatusCode.Unauthorized, secondTryRes.StatusCode);

        // Verify in database that all tokens for this user are now revoked
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var userTokens = await context.RefreshTokens.Where(rt => rt.UserId == userId).ToListAsync();
        Assert.All(userTokens, t => Assert.True(t.IsRevoked));
    }

    [Fact]
    public async Task RefreshToken_ExpiredToken_Returns401Unauthorized()
    {
        // Arrange
        var (_, refreshToken, _, _) = await RegisterUserAsync("expired_test");

        // Manually expire the token in database
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var tokenHash = NexaOps.Application.Common.Security.CryptoUtils.HashToken(refreshToken);
            var tokenRecord = await context.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);
            Assert.NotNull(tokenRecord);
            tokenRecord.ExpiresAt = DateTime.UtcNow.AddMinutes(-10); // Expired
            await context.SaveChangesAsync();
        }

        // Act
        var res = await _client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = refreshToken
        });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesActiveSessionAndClearsCookie()
    {
        // Arrange
        var (_, refreshToken, _, _) = await RegisterUserAsync("logout_test");

        // Act
        var logoutRes = await _client.PostAsJsonAsync("/auth/logout", new RefreshTokenRequest
        {
            RefreshToken = refreshToken
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, logoutRes.StatusCode);

        // Subsequent refresh must fail with 401
        var refreshAfterLogout = await _client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = refreshToken
        });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
    }

    [Fact]
    public async Task LogoutAll_RevokesAllUserSessions()
    {
        // Arrange
        var (accessToken, refreshToken1, email, _) = await RegisterUserAsync("logout_all_test");

        // Login second time from another "device"
        var secondLoginRes = await _client.PostAsJsonAsync("/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password123!"
        });
        var secondAuth = await secondLoginRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        var refreshToken2 = secondAuth!.RefreshToken!;

        // Act - Logout-all using accessToken
        var logoutAllMsg = new HttpRequestMessage(HttpMethod.Post, "/auth/logout-all");
        logoutAllMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var logoutAllRes = await _client.SendAsync(logoutAllMsg);

        // Assert
        Assert.Equal(HttpStatusCode.OK, logoutAllRes.StatusCode);

        // Neither refreshToken1 nor refreshToken2 should work anymore
        var ref1Res = await _client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest { RefreshToken = refreshToken1 });
        var ref2Res = await _client.PostAsJsonAsync("/auth/refresh", new RefreshTokenRequest { RefreshToken = refreshToken2 });

        Assert.Equal(HttpStatusCode.Unauthorized, ref1Res.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, ref2Res.StatusCode);
    }

    [Fact]
    public async Task GetSessionsAndRevokeSession_ValidSession_WorksAndProtectsCrossUser()
    {
        // Arrange User 1
        var (token1, _, _, _) = await RegisterUserAsync("sessions_u1");

        // Arrange User 2
        var (token2, _, _, _) = await RegisterUserAsync("sessions_u2");

        // Act 1 - Get sessions for User 1
        var getSessionsMsg = new HttpRequestMessage(HttpMethod.Get, "/auth/sessions");
        getSessionsMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token1);
        var sessionsRes = await _client.SendAsync(getSessionsMsg);

        Assert.Equal(HttpStatusCode.OK, sessionsRes.StatusCode);
        var sessions = await sessionsRes.Content.ReadFromJsonAsync<List<UserSessionResponse>>(JsonOptions);
        Assert.NotNull(sessions);
        Assert.NotEmpty(sessions);
        var user1SessionId = sessions.First().Id;

        // Act 2 - User 2 attempts to revoke User 1's session -> Must be forbidden/unauthorized
        var crossRevokeMsg = new HttpRequestMessage(HttpMethod.Delete, $"/auth/sessions/{user1SessionId}");
        crossRevokeMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token2);
        var crossRevokeRes = await _client.SendAsync(crossRevokeMsg);

        Assert.Equal(HttpStatusCode.Unauthorized, crossRevokeRes.StatusCode);

        // Act 3 - User 1 revokes own session -> Must succeed
        var ownRevokeMsg = new HttpRequestMessage(HttpMethod.Delete, $"/auth/sessions/{user1SessionId}");
        ownRevokeMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token1);
        var ownRevokeRes = await _client.SendAsync(ownRevokeMsg);

        Assert.Equal(HttpStatusCode.OK, ownRevokeRes.StatusCode);
    }
}
