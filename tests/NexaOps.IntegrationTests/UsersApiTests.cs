using System.Net;
using System.Net.Http.Json;
using NexaOps.Application.Common.Models;
using NexaOps.Application.Users.DTOs;
using NexaOps.Domain.Enums;
using NexaOps.IntegrationTests.Common;

namespace NexaOps.IntegrationTests;

public class UsersApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UsersApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateUser_ValidPayload_Returns201Created()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Name = "John Doe",
            Email = $"john.{Guid.NewGuid()}@nexaops.com",
            Password = "password123",
            Role = UserRole.Admin
        };

        // Act
        var response = await _client.PostAsJsonAsync("/users", request);

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
        var email = $"duplicate.{Guid.NewGuid()}@nexaops.com";
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

        // Act
        var response1 = await _client.PostAsJsonAsync("/users", request1);
        Assert.Equal(HttpStatusCode.Created, response1.StatusCode);

        var response2 = await _client.PostAsJsonAsync("/users", request2);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);
    }

    [Fact]
    public async Task GetAllUsers_ReturnsPagedResult()
    {
        // Act
        var response = await _client.GetAsync("/users?page=1&limit=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<UserResponse>>();
        Assert.NotNull(result);
        Assert.NotNull(result.Items);
        Assert.NotNull(result.Meta);
    }

    [Fact]
    public async Task GetUserById_ExistingUser_Returns200OK()
    {
        // Arrange
        var createRequest = new CreateUserRequest
        {
            Name = "Find Me",
            Email = $"findme.{Guid.NewGuid()}@nexaops.com",
            Password = "password123"
        };
        var createResponse = await _client.PostAsJsonAsync("/users", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<UserResponse>();

        // Act
        var response = await _client.GetAsync($"/users/{created!.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
    }

    [Fact]
    public async Task UpdateUser_ExistingUser_ReturnsUpdatedUser()
    {
        // Arrange
        var createRequest = new CreateUserRequest
        {
            Name = "Old Name",
            Email = $"update.{Guid.NewGuid()}@nexaops.com",
            Password = "password123"
        };
        var createResponse = await _client.PostAsJsonAsync("/users", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<UserResponse>();

        var updateRequest = new UpdateUserRequest
        {
            Name = "New Updated Name",
            Role = UserRole.Admin
        };

        // Act
        var patchResponse = await _client.PatchAsJsonAsync($"/users/{created!.Id}", updateRequest);

        // Assert
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);
        var updated = await patchResponse.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(updated);
        Assert.Equal("New Updated Name", updated.Name);
        Assert.Equal("admin", updated.Role);
    }

    [Fact]
    public async Task DeleteUser_ExistingUser_Returns200OK()
    {
        // Arrange
        var createRequest = new CreateUserRequest
        {
            Name = "To Be Deleted",
            Email = $"delete.{Guid.NewGuid()}@nexaops.com",
            Password = "password123"
        };
        var createResponse = await _client.PostAsJsonAsync("/users", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<UserResponse>();

        // Act
        var deleteResponse = await _client.DeleteAsync($"/users/{created!.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        // Verify deleted
        var getResponse = await _client.GetAsync($"/users/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }
}
