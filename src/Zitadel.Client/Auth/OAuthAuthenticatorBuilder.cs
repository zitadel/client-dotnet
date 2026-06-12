// Zitadel SDK
// Bespoke OAuth authenticator builder base.

namespace Zitadel.Client.Auth;

/// <summary>
/// Base builder shared by the concrete OAuth authenticators.
/// </summary>
/// <typeparam name="T">The concrete builder type for fluent chaining.</typeparam>
public abstract class OAuthAuthenticatorBuilder<T>
    where T : OAuthAuthenticatorBuilder<T>
{
    /// <summary>The OpenID discovery helper for the target host.</summary>
    protected OpenId OpenId { get; }

    /// <summary>The space-delimited scope string for the token request.</summary>
    protected string Scope { get; private set; } = OAuthAuthenticator.DefaultScope;

    /// <summary>
    /// Constructs an OAuthAuthenticatorBuilder.
    /// </summary>
    /// <param name="host">The base URL for the API endpoints.</param>
    protected OAuthAuthenticatorBuilder(string host)
    {
        OpenId = new OpenId(host);
    }

    /// <summary>
    /// Overrides the default scopes.
    /// </summary>
    /// <param name="authScopes">A set of scopes for the token request.</param>
    /// <returns>This builder.</returns>
    public T Scopes(HashSet<string> authScopes)
    {
        Scope = string.Join(' ', authScopes);
        return (T)this;
    }
}
