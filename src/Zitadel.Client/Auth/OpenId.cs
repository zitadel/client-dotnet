// Zitadel SDK
// Bespoke authentication helper aligned with the generated IApiClient transport.

using System.Text.Json;

namespace Zitadel.Client.Auth;

/// <summary>
/// Resolves the OpenID Connect discovery document for a Zitadel host.
///
/// <para>Discovery is performed lazily through the shared <see cref="IApiClient"/>
/// (so it inherits the SDK's proxy / TLS / timeout configuration) rather than
/// eagerly in the constructor. The host is captured at construction time; the
/// <c>token_endpoint</c> is fetched the first time
/// <see cref="GetTokenEndpoint"/> is called.</para>
/// </summary>
public class OpenId
{
    private readonly string _hostname;
    private readonly object _lock = new();
    private volatile Uri? _tokenEndpoint;

    /// <summary>
    /// Constructs an OpenId discovery helper for the given hostname.
    /// </summary>
    /// <param name="hostname">The hostname of the OpenID provider.</param>
    public OpenId(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
        {
            throw new ArgumentException("Hostname cannot be empty.", nameof(hostname));
        }

        _hostname = hostname;
        HostEndpoint = BuildHostname(hostname);
    }

    /// <summary>
    /// The base host endpoint URL.
    /// </summary>
    public Uri HostEndpoint { get; }

    /// <summary>
    /// Returns the OAuth2 token endpoint, resolving the discovery document via
    /// the shared API client on first access.
    /// </summary>
    /// <param name="apiClient">The shared API client used for the discovery request.</param>
    /// <returns>The resolved token endpoint URL.</returns>
    public Uri GetTokenEndpoint(IApiClient apiClient)
    {
        Uri? resolved = _tokenEndpoint;
        if (resolved == null)
        {
            lock (_lock)
            {
                resolved = _tokenEndpoint;
                if (resolved == null)
                {
                    resolved = Resolve(apiClient);
                    _tokenEndpoint = resolved;
                }
            }
        }

        return resolved;
    }

    private Uri Resolve(IApiClient apiClient)
    {
        ArgumentNullException.ThrowIfNull(apiClient);

        try
        {
            Uri wellKnown = BuildWellKnownUrl(_hostname);
            ApiHttpResponse response = apiClient
                .SendRequestAsync(
                    "GET",
                    wellKnown,
                    new Dictionary<string, string> { ["Accept"] = "application/json" },
                    null
                )
                .GetAwaiter()
                .GetResult();

            if (response.StatusCode is < 200 or >= 300)
            {
                throw new ApiException(
                    $"Failed to fetch OpenID configuration: HTTP {response.StatusCode}"
                );
            }

            using JsonDocument doc = JsonDocument.Parse(response.Body);
            if (!doc.RootElement.TryGetProperty("token_endpoint", out JsonElement element))
            {
                throw new ApiException("OpenID configuration did not contain a token_endpoint.");
            }

            string? endpoint = element.GetString();
            if (string.IsNullOrEmpty(endpoint))
            {
                throw new ApiException("OpenID configuration did not contain a token_endpoint.");
            }

            return new Uri(endpoint);
        }
        catch (Exception e) when (e is not ApiException)
        {
            throw new ApiException($"Failed to resolve OpenID configuration: {e.Message}", e);
        }
    }

    internal static Uri BuildHostname(string hostname)
    {
        string normalized = hostname.Trim();
        if (
            !normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        )
        {
            normalized = "https://" + normalized;
        }

        return new Uri(normalized);
    }

    private static Uri BuildWellKnownUrl(string hostname)
    {
        return new Uri(BuildHostname(hostname), "/.well-known/openid-configuration");
    }
}
