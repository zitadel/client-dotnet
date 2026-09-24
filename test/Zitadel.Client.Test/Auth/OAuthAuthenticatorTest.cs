// Zitadel SDK
// Verifies the OAuth authenticator contract: redaction, host validation,
// OpenID discovery failures and token endpoint failures.

using System.Reflection;
using Xunit;
using Zitadel.Client;
using Zitadel.Client.Auth;
using Zitadel.Client.Errors;

namespace Test.Auth;

/// <summary>
/// Verifies the <see cref="OAuthAuthenticator"/> contract shared by every OAuth
/// authenticator: token redaction, host validation, OpenID discovery failures
/// and token endpoint failures.
/// <para>No test reaches a real host. HTTP-level cases use an in-memory
/// <see cref="IApiClient"/> that answers with canned responses; the transport
/// case uses the real <see cref="DefaultApiClient"/> against a local port
/// nothing listens on.</para>
/// </summary>
public class OAuthAuthenticatorTest
{
    private const string Secret = "super-secret-value-should-not-appear";

    private const string Host = "https://zitadel.example.com";

    private const string Discovery =
        "{\"issuer\":\"https://zitadel.example.com\","
        + "\"token_endpoint\":\"https://zitadel.example.com/oauth/v2/token\"}";

    /// <summary>
    /// An <see cref="IApiClient"/> that records token requests and answers
    /// discovery and token calls with canned responses.
    /// </summary>
    private sealed class StubApiClient(ApiHttpResponse discovery, ApiHttpResponse token)
        : IApiClient
    {
        public List<string> Bodies { get; } = [];

        public Task<ApiHttpResponse> SendRequestAsync(
            string method,
            Uri url,
            Dictionary<string, string> headers,
            object? body,
            bool noRedirect = false
        )
        {
            if (
                url.AbsolutePath.EndsWith(
                    "/.well-known/openid-configuration",
                    StringComparison.Ordinal
                )
            )
            {
                return Task.FromResult(discovery);
            }
            Bodies.Add(body?.ToString() ?? string.Empty);
            return Task.FromResult(token);
        }
    }

    private static ApiHttpResponse Response(int status, string body)
    {
        return new ApiHttpResponse(status, body, new Dictionary<string, string>());
    }

    private static ClientCredentialsAuthenticator Stubbed(IApiClient apiClient, string host = Host)
    {
        ClientCredentialsAuthenticator authenticator = ClientCredentialsAuthenticator
            .CreateBuilder(host, "client-1", "client-secret")
            .Build();
        authenticator.SetApiClient(apiClient);
        return authenticator;
    }

    private static ClientCredentialsAuthenticator TokenStubbed(int status, string body)
    {
        return Stubbed(new StubApiClient(Response(200, Discovery), Response(status, body)));
    }

    /// <summary>
    /// Seeds a known access token into the cache and asserts that
    /// <see cref="object.ToString"/> redacts it.
    /// </summary>
    [Fact]
    public void RedactsSecret()
    {
        ClientCredentialsAuthenticator authenticator = ClientCredentialsAuthenticator
            .CreateBuilder(Host, "client-1", "client-secret")
            .Build();
        FieldInfo field =
            typeof(OAuthAuthenticator).GetField(
                "_accessToken",
                BindingFlags.Instance | BindingFlags.NonPublic
            ) ?? throw new InvalidOperationException("OAuthAuthenticator._accessToken not found.");
        field.SetValue(authenticator, Secret);

        string rendered = authenticator.ToString();

        Assert.DoesNotContain(Secret, rendered);
        Assert.Contains("***", rendered);
    }

    [Fact]
    public void MintsAndCachesToken()
    {
        StubApiClient apiClient = new(
            Response(200, Discovery),
            Response(200, "{\"access_token\":\"t0k3n\",\"expires_in\":3600}")
        );
        ClientCredentialsAuthenticator authenticator = Stubbed(apiClient);

        Assert.Equal("t0k3n", authenticator.GetAuthToken());
        Assert.Equal("Bearer t0k3n", authenticator.GetAuthHeaders()["Authorization"]);
        Assert.Single(apiClient.Bodies);
        Assert.StartsWith(
            "grant_type=client_credentials&scope=openid",
            apiClient.Bodies[0],
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("ftp://example.com")]
    [InlineData("https://")]
    public void RejectsBadHost(string host)
    {
        Assert.Throws<ArgumentException>(() =>
            ClientCredentialsAuthenticator.CreateBuilder(host, "client-1", "client-secret")
        );
    }

    [Fact]
    public void RequiresApiClient()
    {
        ClientCredentialsAuthenticator authenticator = ClientCredentialsAuthenticator
            .CreateBuilder(Host, "client-1", "client-secret")
            .Build();

        Assert.Throws<InvalidOperationException>(() => authenticator.GetAuthToken());
    }

    [Fact]
    public void DiscoveryUnreachable()
    {
        using DefaultApiClient apiClient = new(TransportOptions.Builder().Build());
        ClientCredentialsAuthenticator authenticator = Stubbed(apiClient, "http://127.0.0.1:1");

        NetworkException error = Assert.Throws<NetworkException>(() =>
            authenticator.GetAuthToken()
        );
        Assert.IsAssignableFrom<ApiException>(error);
        Assert.Equal(0, error.StatusCode);
    }

    [Fact]
    public void DiscoveryNon2xx()
    {
        NotFoundException notFound = Assert.Throws<NotFoundException>(() =>
            Stubbed(new StubApiClient(Response(404, "{}"), Response(200, "{}"))).GetAuthToken()
        );
        Assert.Equal(404, notFound.StatusCode);
        Assert.IsAssignableFrom<ZitadelException>(notFound);

        Assert.Throws<InternalServerErrorException>(() =>
            Stubbed(new StubApiClient(Response(500, "{}"), Response(200, "{}"))).GetAuthToken()
        );
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"issuer\":\"x\"}")]
    public void DiscoveryMalformed(string body)
    {
        SerializationException error = Assert.Throws<SerializationException>(() =>
            Stubbed(new StubApiClient(Response(200, body), Response(200, "{}"))).GetAuthToken()
        );
        Assert.IsAssignableFrom<ZitadelException>(error);
    }

    [Fact]
    public void TokenEndpointRejects()
    {
        OAuth2ServerException error = Assert.Throws<OAuth2ServerException>(() =>
            TokenStubbed(401, "{\"error\":\"invalid_client\",\"error_description\":\"bad\"}")
                .GetAuthToken()
        );
        Assert.Equal(401, error.StatusCode);
        Assert.Equal("invalid_client", error.Code);
        Assert.Equal("bad", error.Description);
        Assert.IsAssignableFrom<ZitadelException>(error);

        OAuth2ServerException raw = Assert.Throws<OAuth2ServerException>(() =>
            TokenStubbed(503, "down").GetAuthToken()
        );
        Assert.Equal(503, raw.StatusCode);
        Assert.Equal("down", raw.RawBody);
    }

    [Theory]
    [InlineData("{\"token_type\":\"Bearer\"}")]
    [InlineData("not json")]
    [InlineData("{\"access_token\":\"\"}")]
    public void TokenEndpointUnusable(string body)
    {
        OAuth2TokenException error = Assert.Throws<OAuth2TokenException>(() =>
            TokenStubbed(200, body).GetAuthToken()
        );
        Assert.IsAssignableFrom<ZitadelException>(error);
    }

    [Fact]
    public void RejectsBadScopes()
    {
        ClientCredentialsAuthenticatorBuilder builder =
            ClientCredentialsAuthenticator.CreateBuilder(Host, "client-1", "client-secret");

        Assert.Throws<ArgumentException>(() => builder.Scopes());
        Assert.Throws<ArgumentException>(() => builder.Scopes("open id"));
        Assert.Throws<ArgumentException>(() => builder.Scopes(""));
    }

    [Fact]
    public void JoinsScopes()
    {
        StubApiClient apiClient = new(
            Response(200, Discovery),
            Response(200, "{\"access_token\":\"t\"}")
        );
        ClientCredentialsAuthenticator authenticator = ClientCredentialsAuthenticator
            .CreateBuilder(Host, "client-1", "client-secret")
            .Scopes("openid", "profile", "openid")
            .Build();
        authenticator.SetApiClient(apiClient);

        authenticator.GetAuthToken();

        Assert.Contains("&scope=openid%20profile&", apiClient.Bodies[0], StringComparison.Ordinal);
    }
}
