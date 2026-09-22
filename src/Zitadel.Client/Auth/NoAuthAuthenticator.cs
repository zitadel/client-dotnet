// Zitadel SDK
// Bespoke authenticator aligned with the generated IAuthenticator interface.

namespace Zitadel.Client.Auth;

/// <summary>
/// Authenticator implementation for testing purposes.
/// <para>This strategy applies no authentication and returns empty headers.</para>
/// </summary>
public class NoAuthAuthenticator : BaseAuthenticator
{
    private readonly string _host;

    /// <summary>
    /// Constructs a NoAuthAuthenticator.
    /// </summary>
    /// <param name="host">The base URL for the API endpoints.</param>
    /// <exception cref="ArgumentException">If the host is not a valid http or https URL.</exception>
    public NoAuthAuthenticator(string host)
    {
        _host = new OpenId(host).HostEndpoint;
    }

    /// <summary>
    /// Constructs a NoAuthAuthenticator for <c>http://localhost</c>.
    /// </summary>
    public NoAuthAuthenticator()
        : this("http://localhost") { }

    /// <inheritdoc/>
    public override string GetHost()
    {
        return _host;
    }

    /// <inheritdoc/>
    public override Dictionary<string, string> GetAuthHeaders()
    {
        return [];
    }
}
