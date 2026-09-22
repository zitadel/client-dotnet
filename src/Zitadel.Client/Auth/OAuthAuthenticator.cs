// Zitadel SDK
// Bespoke OAuth authenticator aligned with the generated IHttpAwareAuthenticator
// interface and the shared IApiClient transport.

using System.Text.Json;
using Zitadel.Client.Errors;

namespace Zitadel.Client.Auth;

/// <summary>
/// Abstract base class for OAuth-based, token-minting authenticators.
/// <para>Mints a bearer token by POSTing an OAuth2 grant (client-credentials or
/// a signed JWT-bearer assertion) to the provider's token endpoint, then
/// attaches the resulting access token on every API request. The minted token
/// is cached together with its expiry and only re-minted once it is within the
/// refresh skew of expiring.</para>
/// <para>Token-minting requires an outbound HTTP call, so this class implements
/// <see cref="IHttpAwareAuthenticator"/>: the shared <see cref="IApiClient"/> is
/// injected by the <c>Zitadel</c> constructor and both OpenID discovery and the
/// token POST are sent through it. A token request fails with:</para>
/// <list type="bullet">
///   <item><description><see cref="InvalidOperationException"/> when no
///   <see cref="IApiClient"/> has been injected;</description></item>
///   <item><description><see cref="NetworkException"/> or
///   <see cref="NetworkTimeoutException"/> when no HTTP response arrived;</description></item>
///   <item><description><see cref="OAuth2ServerException"/> when the token
///   endpoint answered with a non-2xx status;</description></item>
///   <item><description><see cref="OAuth2TokenException"/> when it answered 2xx
///   without a usable access token.</description></item>
/// </list>
/// </summary>
public abstract class OAuthAuthenticator : BaseAuthenticator, IHttpAwareAuthenticator
{
    /// <summary>The default scopes requested when none are configured.</summary>
    public const string DefaultScope = "openid urn:zitadel:iam:org:project:id:zitadel:aud";

    private const int RefreshSkewSeconds = 300;

    private readonly OpenId _openId;
    private readonly object _gate = new();
    private Task<string>? _pending;
    private volatile IApiClient? _apiClient;
    private volatile string? _accessToken;
    private DateTimeOffset? _expiresAt;

    /// <summary>
    /// Constructs an OAuthAuthenticator.
    /// </summary>
    /// <param name="openId">The OpenID discovery helper for the target host.</param>
    /// <param name="scope">The space-delimited scope string for the token request.</param>
    protected OAuthAuthenticator(OpenId openId, string scope)
    {
        _openId = openId;
        Scope = scope;
    }

    /// <summary>The space-delimited scope string for the token request.</summary>
    protected string Scope { get; }

    /// <inheritdoc/>
    public void SetApiClient(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <inheritdoc/>
    public override string GetHost()
    {
        return _openId.HostEndpoint;
    }

    /// <summary>
    /// Returns a valid access token, minting (or re-minting) one if the cache is
    /// empty or within the refresh skew of expiring.
    /// </summary>
    /// <returns>A valid access token.</returns>
    public string GetAuthToken()
    {
        return GetAuthTokenAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Exchanges the configured grant for a fresh access token and caches it.
    /// </summary>
    /// <returns>The freshly minted access token.</returns>
    public string RefreshToken()
    {
        return RefreshTokenAsync().GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public override Dictionary<string, string> GetAuthHeaders()
    {
        return new() { ["Authorization"] = "Bearer " + GetAuthToken() };
    }

    /// <inheritdoc/>
    public override async Task<Dictionary<string, string>> GetAuthHeadersAsync()
    {
        string token = await GetAuthTokenAsync().ConfigureAwait(false);
        return new() { ["Authorization"] = "Bearer " + token };
    }

    private Task<string> GetAuthTokenAsync()
    {
        string? current = _accessToken;
        return current != null && !IsStale() ? Task.FromResult(current) : RefreshTokenAsync();
    }

    private Task<string> RefreshTokenAsync()
    {
        lock (_gate)
        {
            if (_pending == null || _pending.IsCompleted)
            {
                _pending = MintTokenAsync();
            }
            return _pending;
        }
    }

    private bool IsStale()
    {
        DateTimeOffset? expiresAt = _expiresAt;
        return expiresAt != null
            && DateTimeOffset.UtcNow >= expiresAt.Value.AddSeconds(-RefreshSkewSeconds);
    }

    private async Task<string> MintTokenAsync()
    {
        IApiClient client =
            _apiClient
            ?? throw new InvalidOperationException(
                "OAuthAuthenticator has no ApiClient; use it through the Zitadel client, "
                    + "which injects one before the first token request."
            );

        Dictionary<string, string> form = new()
        {
            ["grant_type"] = GetGrantType(),
            ["scope"] = Scope,
        };
        foreach (KeyValuePair<string, string> entry in GetTokenRequestParams())
        {
            form[entry.Key] = entry.Value;
        }

        string tokenEndpoint = await _openId.GetTokenEndpointAsync(client).ConfigureAwait(false);
        // never replay a token POST across a redirect: a malicious 307/308 could
        // otherwise leak the assertion or secret.
        ApiHttpResponse response = await client
            .SendRequestAsync(
                "POST",
                new Uri(tokenEndpoint),
                new Dictionary<string, string>
                {
                    ["Content-Type"] = "application/x-www-form-urlencoded",
                    ["Accept"] = "application/json",
                },
                EncodeForm(form),
                true
            )
            .ConfigureAwait(false);

        int status = response.StatusCode;
        if (status is < 200 or >= 300)
        {
            throw ServerError(status, response.Body);
        }

        JsonElement? payload = ParseObject(response.Body);
        if (payload == null)
        {
            throw new OAuth2TokenException("Token response is not a JSON object");
        }
        if (
            !payload.Value.TryGetProperty("access_token", out JsonElement tokenElement)
            || tokenElement.ValueKind != JsonValueKind.String
            || string.IsNullOrEmpty(tokenElement.GetString())
        )
        {
            throw new OAuth2TokenException("Token response missing or empty access_token field");
        }
        string accessToken = tokenElement.GetString()!;
        _expiresAt =
            payload.Value.TryGetProperty("expires_in", out JsonElement expiresIn)
            && expiresIn.ValueKind == JsonValueKind.Number
            && expiresIn.TryGetDouble(out double seconds)
            && seconds > 0
                ? DateTimeOffset.UtcNow.AddSeconds(seconds)
                : null;
        _accessToken = accessToken;
        return accessToken;
    }

    private static JsonElement? ParseObject(string body)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.Clone()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static OAuth2ServerException ServerError(int status, string body)
    {
        JsonElement? payload = ParseObject(body);
        if (
            payload == null
            || !payload.Value.TryGetProperty("error", out JsonElement code)
            || code.ValueKind != JsonValueKind.String
            || string.IsNullOrEmpty(code.GetString())
        )
        {
            return new OAuth2ServerException(status, null, null, null, body);
        }
        return new OAuth2ServerException(
            status,
            code.GetString(),
            StringProperty(payload.Value, "error_description"),
            StringProperty(payload.Value, "error_uri"),
            body
        );
    }

    private static string? StringProperty(JsonElement element, string name)
    {
        return
            element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
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
    /// Returns <c>***</c> when a token is cached and <c>null</c> otherwise.
    /// </summary>
    /// <returns>The masked token.</returns>
    protected string MaskedToken()
    {
        return _accessToken == null ? "null" : "***";
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"{GetType().Name}(host={GetHost()}, scope={Scope}, accessToken={MaskedToken()})";
    }

    /// <summary>
    /// Throws <see cref="ArgumentException"/> when the value is null or blank.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="label">The name used in the error message.</param>
    /// <returns>The value.</returns>
    internal static string RequireText(string? value, string label)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{label} cannot be empty.")
            : value;
    }

    /// <summary>
    /// Returns the OAuth2 <c>grant_type</c> value for the token request.
    /// </summary>
    /// <returns>The grant type.</returns>
    protected abstract string GetGrantType();

    /// <summary>
    /// Returns grant-specific token-request parameters (e.g. assertion).
    /// </summary>
    /// <returns>Additional form parameters for the token request.</returns>
    protected abstract Dictionary<string, string> GetTokenRequestParams();
}
