// Zitadel SDK
// Bespoke authenticator aligned with the generated IHttpAwareAuthenticator interface.

namespace Zitadel.Client.Auth;

/// <summary>
/// OAuth2 Client Credentials Authenticator.
/// <para>Implements the OAuth2 client credentials grant (RFC 6749 §4.4) to obtain
/// an access token. Client credentials are transmitted in the request body (the
/// <c>client_secret_post</c> method) on the token request.</para>
/// </summary>
public class ClientCredentialsAuthenticator : OAuthAuthenticator
{
    private const string GrantType = "client_credentials";

    private readonly string _clientId;
    private readonly string _clientSecret;

    /// <summary>
    /// Constructs a ClientCredentialsAuthenticator.
    /// </summary>
    /// <param name="openId">The OpenID discovery helper for the target host.</param>
    /// <param name="clientId">The OAuth2 client identifier.</param>
    /// <param name="clientSecret">The OAuth2 client secret.</param>
    /// <param name="scope">The space-delimited scope string for the token request.</param>
    internal ClientCredentialsAuthenticator(
        OpenId openId,
        string clientId,
        string clientSecret,
        string scope
    )
        : base(openId, scope)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
    }

    /// <summary>
    /// Returns a new builder for a ClientCredentialsAuthenticator.
    /// </summary>
    /// <param name="host">The base URL for the API endpoints.</param>
    /// <param name="clientId">The OAuth2 client identifier.</param>
    /// <param name="clientSecret">The OAuth2 client secret.</param>
    /// <returns>A new builder.</returns>
    /// <exception cref="ArgumentException">If the host is not a valid http or https URL,
    /// or the client identifier or secret is empty.</exception>
    public static ClientCredentialsAuthenticatorBuilder CreateBuilder(
        string host,
        string clientId,
        string clientSecret
    )
    {
        return new ClientCredentialsAuthenticatorBuilder(host, clientId, clientSecret);
    }

    /// <inheritdoc/>
    protected override string GetGrantType()
    {
        return GrantType;
    }

    /// <inheritdoc/>
    protected override Dictionary<string, string> GetTokenRequestParams()
    {
        return new() { ["client_id"] = _clientId, ["client_secret"] = _clientSecret };
    }

    /// <summary>
    /// Returns a string representation with the client secret and cached token redacted.
    /// </summary>
    /// <returns>A string representation with the secrets redacted.</returns>
    public override string ToString()
    {
        return $"{nameof(ClientCredentialsAuthenticator)}(host={GetHost()}, "
            + $"clientId={_clientId}, clientSecret=***, scope={Scope}, accessToken={MaskedToken()})";
    }
}

/// <summary>
/// Builder for <see cref="ClientCredentialsAuthenticator"/>.
/// </summary>
public class ClientCredentialsAuthenticatorBuilder
    : OAuthAuthenticatorBuilder<ClientCredentialsAuthenticatorBuilder>
{
    private readonly string _clientId;
    private readonly string _clientSecret;

    /// <summary>
    /// Initialises the builder.
    /// </summary>
    /// <param name="host">The base URL for the API endpoints.</param>
    /// <param name="clientId">The OAuth2 client identifier.</param>
    /// <param name="clientSecret">The OAuth2 client secret.</param>
    internal ClientCredentialsAuthenticatorBuilder(
        string host,
        string clientId,
        string clientSecret
    )
        : base(host)
    {
        _clientId = OAuthAuthenticator.RequireText(clientId, "Client ID");
        _clientSecret = OAuthAuthenticator.RequireText(clientSecret, "Client secret");
    }

    /// <summary>
    /// Builds the ClientCredentialsAuthenticator.
    /// </summary>
    /// <returns>A new ClientCredentialsAuthenticator.</returns>
    public ClientCredentialsAuthenticator Build()
    {
        return new ClientCredentialsAuthenticator(OpenId, _clientId, _clientSecret, Scope);
    }
}
