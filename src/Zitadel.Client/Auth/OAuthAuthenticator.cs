// Zitadel SDK
// Bespoke OAuth authenticator aligned with the generated IHttpAwareAuthenticator
// interface and the shared IApiClient transport.

using System.Text.Json;

namespace Zitadel.Client.Auth;

/// <summary>
/// Abstract base class for OAuth-based authenticators.
///
/// <para>Provides common functionality for OAuth authenticators: token caching
/// and Authorization-header construction. The token-endpoint POST is performed
/// through the shared <see cref="IApiClient"/> injected via
/// <see cref="SetApiClient"/>, so token exchange inherits the SDK's transport
/// configuration (proxy, TLS, timeouts) instead of using a separate OAuth
/// client library.</para>
///
/// <para>Subclasses supply the OAuth2 <c>grant_type</c> and any grant-specific
/// form parameters (client credentials, signed JWT assertion, etc.).</para>
/// </summary>
public abstract class OAuthAuthenticator : BaseAuthenticator, IHttpAwareAuthenticator
{
    /// <summary>Default Zitadel scopes for project-audience tokens.</summary>
    public const string DefaultScope = "openid urn:zitadel:iam:org:project:id:zitadel:aud";

    /// <summary>The space-delimited scope string for the token request.</summary>
    protected string Scope { get; }

    private readonly OpenId _openId;
    private readonly object _lock = new();
    private volatile IApiClient? _apiClient;
    private volatile Token? _token;

    /// <summary>
    /// Constructs an OAuthAuthenticator.
    /// </summary>
    /// <param name="openId">The OpenID discovery helper for the target host.</param>
    /// <param name="scope">The space-delimited scope string for the token request.</param>
    protected OAuthAuthenticator(OpenId openId, string? scope)
    {
        _openId = openId;
        Scope = string.IsNullOrEmpty(scope) ? DefaultScope : scope;
    }

    /// <inheritdoc/>
    public void SetApiClient(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <inheritdoc/>
    public override string GetHost()
    {
        return _openId.HostEndpoint.ToString();
    }

    /// <summary>
    /// Returns the OAuth2 <c>grant_type</c> value for the token request.
    /// </summary>
    protected abstract string GetGrantType();

    /// <summary>
    /// Returns grant-specific form parameters to include in the token request.
    /// </summary>
    protected abstract Dictionary<string, string> GetTokenRequestParams();

    /// <summary>
    /// Returns extra HTTP headers for the token request (for example a
    /// <c>Authorization: Basic</c> header carrying client credentials).
    /// </summary>
    protected virtual Dictionary<string, string> GetTokenRequestHeaders()
    {
        return [];
    }

    /// <summary>
    /// Retrieves a valid bearer token, refreshing it if absent or expired.
    /// </summary>
    public string GetAuthToken()
    {
        Token? current = _token;
        if (current == null || current.IsExpired())
        {
            lock (_lock)
            {
                current = _token;
                if (current == null || current.IsExpired())
                {
                    current = RefreshToken();
                    _token = current;
                }
            }
        }

        return current.AccessToken;
    }

    /// <inheritdoc/>
    public override Dictionary<string, string> GetAuthHeaders()
    {
        return new() { ["Authorization"] = "Bearer " + GetAuthToken() };
    }

    /// <summary>
    /// Returns a string representation of this authenticator with the cached
    /// access token redacted (rendered as <c>***</c> when present), so the
    /// bearer token is never leaked through logging or diagnostics.
    /// </summary>
    public override string ToString()
    {
        string masked = _token == null ? "null" : "***";
        return $"{GetType().Name}(host={GetHost()}, scope={Scope}, accessToken={masked})";
    }

    /// <summary>
    /// Refreshes the access token by POSTing to the OAuth2 token endpoint
    /// through the shared API client.
    /// </summary>
    private Token RefreshToken()
    {
        IApiClient client =
            _apiClient
            ?? throw new InvalidOperationException(
                "ApiClient has not been injected; token exchange cannot run before SetApiClient()."
            );
        try
        {
            Uri tokenEndpoint = _openId.GetTokenEndpoint(client);

            Dictionary<string, string> form = new()
            {
                ["grant_type"] = GetGrantType(),
                ["scope"] = Scope,
            };
            foreach (KeyValuePair<string, string> entry in GetTokenRequestParams())
            {
                form[entry.Key] = entry.Value;
            }

            Dictionary<string, string> headers = new()
            {
                ["Content-Type"] = "application/x-www-form-urlencoded",
                ["Accept"] = "application/json",
            };
            foreach (KeyValuePair<string, string> entry in GetTokenRequestHeaders())
            {
                headers[entry.Key] = entry.Value;
            }

            // no_redirect: never replay a token POST across a redirect — a
            // malicious 307/308 could otherwise leak the assertion/secret.
            ApiHttpResponse response = client
                .SendRequestAsync("POST", tokenEndpoint, headers, EncodeForm(form), true)
                .GetAwaiter()
                .GetResult();

            if (response.StatusCode is < 200 or >= 300)
            {
                throw new ApiException(
                    $"Token request failed: HTTP {response.StatusCode} {response.Body}"
                );
            }

            using JsonDocument doc = JsonDocument.Parse(response.Body);
            if (!doc.RootElement.TryGetProperty("access_token", out JsonElement accessTokenElement))
            {
                throw new ApiException("Token response did not contain an access_token.");
            }

            string? accessToken = accessTokenElement.GetString();
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new ApiException("Token response did not contain an access_token.");
            }

            long expiresIn = 3600L;
            if (
                doc.RootElement.TryGetProperty("expires_in", out JsonElement expiresElement)
                && expiresElement.ValueKind == JsonValueKind.Number
            )
            {
                expiresIn = expiresElement.GetInt64();
            }

            return new Token(accessToken, DateTime.UtcNow.AddSeconds(expiresIn));
        }
        catch (Exception e) when (e is not ApiException)
        {
            throw new ApiException($"Failed to refresh token: {e.Message}", e);
        }
    }

    private static string EncodeForm(Dictionary<string, string> form)
    {
        return string.Join(
            '&',
            form.Select(entry =>
                $"{Uri.EscapeDataString(entry.Key)}={Uri.EscapeDataString(entry.Value)}"
            )
        );
    }

    /// <summary>
    /// A simple immutable holder for an OAuth access token and its expiry.
    /// </summary>
    private sealed class Token(string accessToken, DateTime expiresAt)
    {
        public string AccessToken { get; } = accessToken;

        private readonly DateTime _expiresAt = expiresAt;

        public bool IsExpired()
        {
            return DateTime.UtcNow > _expiresAt.AddMinutes(-5);
        }
    }
}
