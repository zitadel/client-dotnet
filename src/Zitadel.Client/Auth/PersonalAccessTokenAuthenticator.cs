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
    /// <exception cref="ArgumentException">If the host is not a valid http or https URL
    /// or the token is empty.</exception>
    public PersonalAccessTokenAuthenticator(string host, string token)
    {
        _host = new OpenId(host).HostEndpoint;
        _token = OAuthAuthenticator.RequireText(token, "Token");
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

    /// <summary>
    /// Returns a string representation with the token redacted.
    /// </summary>
    /// <returns>A string representation with the token redacted.</returns>
    public override string ToString()
    {
        return $"{nameof(PersonalAccessTokenAuthenticator)}(host={_host}, token=***)";
    }
}
