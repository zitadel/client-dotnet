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
    /// <param name="host">The base URL for authentication endpoints.</param>
    public NoAuthAuthenticator(string host)
    {
        _host = OpenId.BuildHostname(host).ToString();
    }

    /// <summary>
    /// Constructs a NoAuthAuthenticator targeting <c>localhost</c>.
    /// </summary>
    public NoAuthAuthenticator()
        : this("localhost") { }

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
