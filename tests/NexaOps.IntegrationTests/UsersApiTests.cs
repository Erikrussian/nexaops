using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NexaOps.Application.Common.Interfaces;
using NexaOps.Application.Common.Models;
using NexaOps.Application.Users.DTOs;
using NexaOps.Domain.Entities;
using NexaOps.Domain.Enums;
using NexaOps.Infrastructure.Persistence;
using NexaOps.IntegrationTests.Common;

namespace NexaOps.IntegrationTests;

public class UsersApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public UsersApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> GetAdminTokenAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var jwtGenerator = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Name = "System Admin",
            Email = $"admin_{Guid.NewGuid():N}@nexaops.com",
            PasswordHash = passwordHasher.HashPassword("admin123"),
            Role = UserRole.Admin,
            IsActive = true
        };
        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync();

        return jwtGenerator.GenerateToken(admin);
    }

    [Fact]
    public async Task CreateUser_AsAdmin_ValidPayload_Returns201Created()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var request = new CreateUserRequest
        {
            Name = "John Doe",
            Email = $"john.{Guid.NewGuid():N}@nexaops.com",
            Password = "password123",
            Role = UserRole.Admin
        };

        var msg = new HttpRequestMessage(HttpMethod.Post, "/users")
        {
            Content = JsonContent.Create(request)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(msg);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(created);
        Assert.Equal(request.Name, created.Name);
        Assert.Equal(request.Email.ToLowerInvariant(), created.Email);
        Assert.Equal("admin", created.Role);
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_Returns400BadRequest()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var email = $"duplicate.{Guid.NewGuid():N}@nexaops.com";
        var request1 = new CreateUserRequest
        {
            Name = "First User",
            Email = email,
            Password = "password123"
        };
        var request2 = new CreateUserRequest
        {
            Name = "Second User",
            Email = email,
            Password = "password456"
        };

        var msg1 = new HttpRequestMessage(HttpMethod.Post, "/users")
        {
            Content = JsonContent.Create(request1)
        };
        msg1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response1 = await _client.SendAsync(msg1);
        Assert.Equal(HttpStatusCode.Created, response1.StatusCode);

        var msg2 = new HttpRequestMessage(HttpMethod.Post, "/users")
        {
            Content = JsonContent.Create(request2)
        };
        msg2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response2 = await _client.SendAsync(msg2);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);
    }

    [Fact]
    public async Task GetAllUsers_AsAdmin_ReturnsPagedResult()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var msg = new HttpRequestMessage(HttpMethod.Get, "/users?page=1&limit=10");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(msg);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<UserResponse>>();
        Assert.NotNull(result);
        Assert.NotNull(result.Items);
        Assert.NotNull(result.Meta);
    }

    [Fact]
    public async Task GetUserById_AsAdmin_ExistingUser_Returns200OK()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var createRequest = new CreateUserRequest
        {
            Name = "Find Me",
            Email = $"findme.{Guid.NewGuid():N}@nexaops.com",
            Password = "password123"
        };
        var msgCreate = new HttpRequestMessage(HttpMethod.Post, "/users")
        {
            Content = JsonContent.Create(createRequest)
        };
        msgCreate.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createResponse = await _client.SendAsync(msgCreate);
        var created = await createResponse.Content.ReadFromJsonAsync<UserResponse>();

        // Act
        var msgGet = new HttpRequestMessage(HttpMethod.Get, $"/users/{created!.Id}");
        msgGet.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(msgGet);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
    }

    [Fact]
    public async Task UpdateUser_AsAdmin_ExistingUser_ReturnsUpdatedUser()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var createRequest = new CreateUserRequest
        {
            Name = "Old Name",
            Email = $"update.{Guid.NewGuid():N}@nexaops.com",
            Password = "password123"
        };
        var msgCreate = new HttpRequestMessage(HttpMethod.Post, "/users")
        {
            Content = JsonContent.Create(createRequest)
        };
        msgCreate.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createResponse = await _client.SendAsync(msgCreate);
        var created = await createResponse.Content.ReadFromJsonAsync<UserResponse>();

        var updateRequest = new UpdateUserRequest
        {
            Name = "New Updated Name",
            Role = UserRole.Admin
        };

        var msgUpdate = new HttpRequestMessage(HttpMethod.Patch, $"/users/{created!.Id}")
        {
            Content = JsonContent.Create(updateRequest)
        };
        msgUpdate.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var patchResponse = await _client.SendAsync(msgUpdate);

        // Assert
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);
        var updated = await patchResponse.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(updated);
        Assert.Equal("New Updated Name", updated.Name);
        Assert.Equal("admin", updated.Role);
    }

    [Fact]
    public async Task DeleteUser_AsAdmin_ExistingUser_Returns200OK()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var createRequest = new CreateUserRequest
        {
            Name = "To Be Deleted",
            Email = $"delete.{Guid.NewGuid():N}@nexaops.com",
            Password = "password123"
        };
        var msgCreate = new HttpRequestMessage(HttpMethod.Post, "/users")
        {
            Content = JsonContent.Create(createRequest)
        };
        msgCreate.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createResponse = await _client.SendAsync(msgCreate);
        var created = await createResponse.Content.ReadFromJsonAsync<UserResponse>();

        // Act
        var msgDel = new HttpRequestMessage(HttpMethod.Delete, $"/users/{created!.Id}");
        msgDel.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var deleteResponse = await _client.SendAsync(msgDel);

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        // Verify deleted
        var msgGet = new HttpRequestMessage(HttpMethod.Get, $"/users/{created.Id}");
        msgGet.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var getResponse = await _client.SendAsync(msgGet);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }
}
