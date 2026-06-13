// Zitadel SDK
// Bespoke authenticator aligned with the generated IAuthenticator interface.

namespace Zitadel.Client.Auth;

/// <summary>
/// Personal Access Token Authenticator.
/// <para>Uses a static personal access token for API authentication.</para>
/// </summary>
public class PersonalAccessTokenAuthenticator : BaseAuthenticator
{
    private readonly string _host;
    private readonly string _token;

    /// <summary>
    /// Constructs a PersonalAccessTokenAuthenticator.
    /// </summary>
    /// <param name="host">The base URL for the API endpoints.</param>
    /// <param name="token">The personal access token.</param>
    public PersonalAccessTokenAuthenticator(string host, string token)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(token);
        _host = OpenId.BuildHostname(host).ToString();
        _token = token;
    }

    /// <inheritdoc/>
    public override string GetHost()
    {
        return _host;
    }

    /// <inheritdoc/>
    public override Dictionary<string, string> GetAuthHeaders()
    {
        return new() { ["Authorization"] = "Bearer " + _token };
    }
}
