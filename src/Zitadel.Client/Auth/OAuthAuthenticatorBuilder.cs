// Zitadel SDK
// Bespoke OAuth authenticator builder base.

namespace Zitadel.Client.Auth;

/// <summary>
/// Abstract builder for OAuth authenticators.
/// <para>Holds the OpenID discovery helper for the host and the requested scopes.</para>
/// </summary>
/// <typeparam name="T">The concrete builder type.</typeparam>
public abstract class OAuthAuthenticatorBuilder<T>
    where T : OAuthAuthenticatorBuilder<T>
{
    /// <summary>
    /// Initialises the builder for the given host.
    /// </summary>
    /// <param name="host">The base URL for the OAuth provider.</param>
    /// <exception cref="ArgumentException">If the host is not a valid http or https URL.</exception>
    protected OAuthAuthenticatorBuilder(string host)
    {
        OpenId = new OpenId(host);
    }

    /// <summary>The OpenID discovery helper for the target host.</summary>
    protected OpenId OpenId { get; }

    /// <summary>The space-delimited scope string for the token request.</summary>
    protected string Scope { get; private set; } = OAuthAuthenticator.DefaultScope;

    /// <summary>
    /// Overrides the default scopes. Duplicates are dropped; order is kept.
    /// </summary>
    /// <param name="authScopes">The scopes for the token request.</param>
    /// <returns>The builder instance.</returns>
    /// <exception cref="ArgumentException">If no scope is given, or a scope is empty
    /// or contains whitespace.</exception>
    public T Scopes(params string[] authScopes)
    {
        if (authScopes == null || authScopes.Length == 0)
        {
            throw new ArgumentException("At least one scope is required.", nameof(authScopes));
        }
        foreach (string authScope in authScopes)
        {
            if (string.IsNullOrEmpty(authScope) || authScope.Any(char.IsWhiteSpace))
            {
                throw new ArgumentException(
                    $"Scope must be a non-empty string without whitespace: '{authScope}'",
                    nameof(authScopes)
                );
            }
        }
        Scope = string.Join(' ', authScopes.Distinct(StringComparer.Ordinal));
        return (T)this;
    }
}
