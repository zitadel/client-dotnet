// Zitadel SDK
// Bespoke authenticator aligned with the generated IHttpAwareAuthenticator interface.

namespace Zitadel.Client.Auth;

/// <summary>
/// OAuth2 Client Credentials Authenticator.
/// <para>Implements the OAuth2 client credentials grant (RFC 6749 §4.4) to obtain
/// an access token. Client credentials are transmitted in the request body
/// (the <c>client_secret_post</c> method) as <c>client_id</c> and
/// <c>client_secret</c> form fields, matching the canonical behaviour of the
/// other Zitadel SDKs.</para>
/// </summary>
public class ClientCredentialsAuthenticator : OAuthAuthenticator
{
    private const string GrantType = "client_credentials";

    private readonly string _clientId;
    private readonly string _clientSecret;

    internal ClientCredentialsAuthenticator(
        OpenId openId,
        string clientId,
        string clientSecret,
        string? scope
    )
        : base(openId, scope)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
    }

    /// <summary>
    /// Returns a new builder instance for ClientCredentialsAuthenticator.
    /// </summary>
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
    /// Returns a string representation of this authenticator with the client
    /// secret redacted (rendered as <c>***</c>), so the credential is never
    /// leaked through logging or diagnostics.
    /// </summary>
    public override string ToString()
    {
        return $"{nameof(ClientCredentialsAuthenticator)}(host={GetHost()}, "
            + $"clientId={_clientId}, clientSecret=***)";
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

    internal ClientCredentialsAuthenticatorBuilder(
        string host,
        string clientId,
        string clientSecret
    )
        : base(host)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
    }

    /// <summary>
    /// Builds the ClientCredentialsAuthenticator.
    /// </summary>
    public ClientCredentialsAuthenticator Build()
    {
        return new ClientCredentialsAuthenticator(OpenId, _clientId, _clientSecret, Scope);
    }
}
