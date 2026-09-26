// Zitadel SDK
// Bespoke authentication helper aligned with the generated IApiClient transport.

using System.Text.Json;
using Zitadel.Client.Errors;

namespace Zitadel.Client.Auth;

/// <summary>
/// Resolves the OpenID Connect discovery document for a Zitadel host.
/// <para>The constructor only validates and normalises the host; it performs no
/// I/O. The <c>token_endpoint</c> is fetched through the shared
/// <see cref="IApiClient"/> the first time <see cref="GetTokenEndpointAsync"/>
/// is called, so discovery honours the SDK's proxy, TLS and timeout settings and
/// fails with the same error types as any other request:</para>
/// <list type="bullet">
///   <item><description>no HTTP response: <see cref="NetworkException"/> or
///   <see cref="NetworkTimeoutException"/>;</description></item>
///   <item><description>a non-2xx status: the <see cref="ApiException"/>
///   subclass for that status;</description></item>
///   <item><description>a body that is not a JSON object with a
///   <c>token_endpoint</c>: <see cref="SerializationException"/>.</description></item>
/// </list>
/// </summary>
public class OpenId
{
    private const string WellKnownPath = "/.well-known/openid-configuration";

    private readonly Uri _wellKnownUrl;
    private readonly object _gate = new();
    private Task<string>? _tokenEndpoint;

    /// <summary>
    /// Validates and normalises the host. A host without a scheme gets <c>https://</c>.
    /// </summary>
    /// <param name="host">The Zitadel instance host name or URL.</param>
    /// <exception cref="ArgumentException">If the host is empty, uses a scheme other
    /// than http or https, or is not a valid URL.</exception>
    public OpenId(string host)
    {
        HostEndpoint = NormaliseHost(host);
        _wellKnownUrl = new Uri(new Uri(HostEndpoint), WellKnownPath);
    }

    /// <summary>The normalised host endpoint.</summary>
    public string HostEndpoint { get; }

    private static string NormaliseHost(string? host)
    {
        string trimmed = (host ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Host cannot be empty.", nameof(host));
        }
        if (
            !trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        )
        {
            if (trimmed.Contains("://", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Host must use the http or https scheme: {trimmed}",
                    nameof(host)
                );
            }
            trimmed = "https://" + trimmed;
        }
        if (
            !Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? parsed)
            || string.IsNullOrEmpty(parsed.Host)
        )
        {
            throw new ArgumentException($"Host is not a valid URL: {trimmed}", nameof(host));
        }
        return trimmed;
    }

    /// <summary>
    /// Returns the OAuth2 token endpoint, fetching the discovery document through
    /// the given API client on first access and caching the result.
    /// </summary>
    /// <param name="apiClient">The shared API client used for the discovery request.</param>
    /// <returns>The token endpoint URL.</returns>
    /// <exception cref="ApiException">If discovery fails at the transport or HTTP level.</exception>
    /// <exception cref="SerializationException">If the discovery document is unusable.</exception>
    public Task<string> GetTokenEndpointAsync(IApiClient apiClient)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        lock (_gate)
        {
            if (_tokenEndpoint == null || _tokenEndpoint.IsFaulted || _tokenEndpoint.IsCanceled)
            {
                _tokenEndpoint = DiscoverAsync(apiClient);
            }
            return _tokenEndpoint;
        }
    }

    private async Task<string> DiscoverAsync(IApiClient apiClient)
    {
        Uri url = _wellKnownUrl;
        ApiHttpResponse response = await apiClient
            .SendRequestAsync(
                "GET",
                url,
                new Dictionary<string, string> { ["Accept"] = "application/json" },
                null
            )
            .ConfigureAwait(false);
        int status = response.StatusCode;
        if (status is < 200 or >= 300)
        {
            throw ApiException.FromResponse(status, response.Headers, response.Body);
        }
        JsonElement root;
        try
        {
            using JsonDocument document = JsonDocument.Parse(response.Body);
            root = document.RootElement.Clone();
        }
        catch (JsonException e)
        {
            throw new SerializationException(
                $"OpenID configuration at {url} is not a JSON object",
                e
            );
        }
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new SerializationException($"OpenID configuration at {url} is not a JSON object");
        }
        if (
            !root.TryGetProperty("token_endpoint", out JsonElement endpoint)
            || endpoint.ValueKind != JsonValueKind.String
            || string.IsNullOrEmpty(endpoint.GetString())
        )
        {
            throw new SerializationException(
                $"OpenID configuration at {url} has no valid token_endpoint"
            );
        }
        return endpoint.GetString()!;
    }
}
