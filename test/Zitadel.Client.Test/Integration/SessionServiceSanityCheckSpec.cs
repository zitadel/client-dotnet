// Zitadel SDK
// SessionService integration sanity checks, ported from the other Zitadel SDKs.

using Zitadel.Client.Models;

namespace Zitadel.Client.Test.Integration;

/// <summary>
/// SessionService Integration Tests.
///
/// <para>This suite verifies the Zitadel SessionService API's basic operations
/// using a personal access token:</para>
/// <list type="number">
///   <item><description>Create a session with specified checks and lifetime.</description></item>
///   <item><description>Retrieve the session by ID.</description></item>
///   <item><description>List sessions and ensure the created session appears.</description></item>
///   <item><description>Update the session's lifetime and confirm a new token is returned.</description></item>
///   <item><description>Error when retrieving a non-existent session.</description></item>
/// </list>
///
/// <para>Each test runs in isolation: a fresh session is created in
/// <see cref="InitializeAsync"/> and deleted in <see cref="DisposeAsync"/>.</para>
/// </summary>
[Collection(ZitadelStackCollection.Name)]
public sealed class SessionServiceSanityCheckSpec : IAsyncLifetime
{
    private readonly global::Zitadel.Client.Zitadel _client;
    private SessionServiceCreateSessionResponse _session = new();

    public SessionServiceSanityCheckSpec(ZitadelStackFixture stack)
    {
        ArgumentNullException.ThrowIfNull(stack);
        _client = ZitadelClients.WithAccessToken(stack.BaseUrl, stack.AuthToken);
    }

    /// <summary>Creates a fresh user and session before each test.</summary>
    public async ValueTask InitializeAsync()
    {
        string username = Guid.NewGuid().ToString("N");

        _ = await _client.UserService.AddHumanUserAsync(
            new UserServiceAddHumanUserRequest
            {
                Username = username,
                Profile = new UserServiceSetHumanProfile { GivenName = "John", FamilyName = "Doe" },
                Email = new UserServiceSetHumanEmail
                {
                    Email = $"johndoe{Guid.NewGuid():N}@example.com",
                },
            }
        );

        _session = await _client.SessionService.CreateSessionAsync(
            new SessionServiceCreateSessionRequest
            {
                Checks = new SessionServiceChecks
                {
                    User = new SessionServiceCheckUser { LoginName = username },
                },
                Lifetime = TimeSpan.FromSeconds(18000),
            }
        );
    }

    /// <summary>Deletes the created session after each test.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            _ = await _client.SessionService.DeleteSessionAsync(
                new SessionServiceDeleteSessionRequest
                {
                    SessionId = _session.SessionId ?? string.Empty,
                }
            );
        }
        catch (ApiException)
        {
            // Cleanup errors are ignored.
        }
        _client.Dispose();
    }

    [Fact]
    public async Task RetrievesSessionDetailsById()
    {
        SessionServiceGetSessionResponse response = await _client.SessionService.GetSessionAsync(
            new SessionServiceGetSessionRequest { SessionId = _session.SessionId ?? string.Empty }
        );

        Assert.NotNull(response.Session);
        Assert.Equal(_session.SessionId, response.Session.Id);
    }

    [Fact]
    public async Task IncludesCreatedSessionWhenListing()
    {
        SessionServiceListSessionsResponse response =
            await _client.SessionService.ListSessionsAsync(
                new SessionServiceListSessionsRequest { Queries = [] }
            );

        Assert.NotNull(response.Sessions);
        Assert.Contains(_session.SessionId, response.Sessions.Select(s => s.Id));
    }

    [Fact]
    public async Task UpdatesSessionLifetimeAndReturnsNewToken()
    {
        SessionServiceSetSessionResponse response = await _client.SessionService.SetSessionAsync(
            new SessionServiceSetSessionRequest
            {
                SessionId = _session.SessionId ?? string.Empty,
                Lifetime = TimeSpan.FromSeconds(36000),
            }
        );

        Assert.NotNull(response.SessionToken);
    }

    [Fact]
    public async Task RaisesApiExceptionForNonexistentSession()
    {
        _ = await Assert.ThrowsAnyAsync<ApiException>(() =>
            _client.SessionService.GetSessionAsync(
                new SessionServiceGetSessionRequest
                {
                    SessionId = Guid.NewGuid().ToString(),
                    SessionToken = _session.SessionToken,
                }
            )
        );
    }
}
