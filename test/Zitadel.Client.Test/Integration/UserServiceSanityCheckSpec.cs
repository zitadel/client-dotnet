// Zitadel SDK
// UserService integration sanity checks, ported from the other Zitadel SDKs.

using Zitadel.Client.Models;

namespace Zitadel.Client.Test.Integration;

/// <summary>
/// UserService Integration Tests.
///
/// <para>This suite verifies the Zitadel UserService API's basic operations
/// using a personal access token:</para>
/// <list type="number">
///   <item><description>Create a human user.</description></item>
///   <item><description>Retrieve the user by ID.</description></item>
///   <item><description>List users and ensure the created user appears.</description></item>
///   <item><description>Update the user's email and confirm the change.</description></item>
///   <item><description>Error when retrieving a non-existent user.</description></item>
/// </list>
///
/// <para>Each test runs in isolation: a fresh user is created in the
/// constructor and removed in <see cref="DisposeAsync"/>.</para>
/// </summary>
[Collection(ZitadelStackCollection.Name)]
public sealed class UserServiceSanityCheckSpec : IAsyncLifetime
{
    private readonly global::Zitadel.Client.Zitadel _client;
    private UserServiceAddHumanUserResponse _user = new();

    public UserServiceSanityCheckSpec(ZitadelStackFixture stack)
    {
        ArgumentNullException.ThrowIfNull(stack);
        _client = ZitadelClients.WithAccessToken(stack.BaseUrl, stack.AuthToken);
    }

    /// <summary>Creates a fresh human user before each test.</summary>
    public async ValueTask InitializeAsync()
    {
        UserServiceAddHumanUserRequest request = new()
        {
            Username = Guid.NewGuid().ToString("N"),
            Profile = new UserServiceSetHumanProfile { GivenName = "John", FamilyName = "Doe" },
            Email = new UserServiceSetHumanEmail
            {
                Email = $"johndoe{Guid.NewGuid():N}@example.com",
            },
        };
        _user = await _client.UserService.AddHumanUserAsync(request);
    }

    /// <summary>Deletes the created human user after each test.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            _ = await _client.UserService.DeleteUserAsync(
                new UserServiceDeleteUserRequest { UserId = _user.UserId ?? string.Empty }
            );
        }
        catch (ApiException)
        {
            // Cleanup errors are ignored.
        }
        _client.Dispose();
    }

    [Fact]
    public async Task RetrievesUserDetailsById()
    {
        UserServiceGetUserByIDResponse response = await _client.UserService.GetUserByIDAsync(
            new UserServiceGetUserByIDRequest { UserId = _user.UserId ?? string.Empty }
        );

        Assert.NotNull(response.User);
        Assert.Equal(_user.UserId, response.User.UserId);
    }

    [Fact]
    public async Task IncludesCreatedUserWhenListing()
    {
        UserServiceListUsersResponse response = await _client.UserService.ListUsersAsync(
            new UserServiceListUsersRequest { Queries = [] }
        );

        Assert.NotNull(response.Result);
        Assert.Contains(_user.UserId, response.Result.Select(u => u.UserId));
    }

    [Fact]
    public async Task UpdatesUserEmailAndReflectsInGet()
    {
        _ = await _client.UserService.UpdateHumanUserAsync(
            new UserServiceUpdateHumanUserRequest
            {
                UserId = _user.UserId,
                Email = new UserServiceSetHumanEmail
                {
                    Email = $"updated{Guid.NewGuid():N}@example.com",
                },
            }
        );

        UserServiceGetUserByIDResponse response = await _client.UserService.GetUserByIDAsync(
            new UserServiceGetUserByIDRequest { UserId = _user.UserId ?? string.Empty }
        );

        Assert.NotNull(response.User?.Human?.Email?.Email);
        Assert.Contains("updated", response.User.Human.Email.Email);
    }

    [Fact]
    public async Task RaisesApiExceptionForNonexistentUser()
    {
        _ = await Assert.ThrowsAnyAsync<ApiException>(() =>
            _client.UserService.GetUserByIDAsync(
                new UserServiceGetUserByIDRequest { UserId = Guid.NewGuid().ToString() }
            )
        );
    }
}
