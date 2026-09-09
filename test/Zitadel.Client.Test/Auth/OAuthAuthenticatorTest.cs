// Zitadel SDK
// Verifies that the authenticator's string representation never leaks credentials.

using System.Reflection;
using Xunit;
using Zitadel.Client.Auth;

namespace Test.Auth;

/// <summary>
/// Ensures the abstract <see cref="OAuthAuthenticator"/> base masks the cached
/// access token (rendered as <c>***</c>) in <see cref="object.ToString"/>,
/// matching the redaction behaviour of the other Zitadel SDKs. The token is
/// seeded directly so no network token exchange is required.
/// </summary>
public class OAuthAuthenticatorTest
{
    private const string Secret = "super-secret-value-should-not-appear";

    /// <summary>
    /// A minimal concrete <see cref="OAuthAuthenticator"/> used to exercise the
    /// base-class redaction behaviour without performing a token exchange.
    /// </summary>
    private sealed class TestOAuthAuthenticator(OpenId openId)
        : OAuthAuthenticator(openId, scope: null)
    {
        /// <inheritdoc/>
        protected override string GetGrantType()
        {
            return "client_credentials";
        }

        /// <inheritdoc/>
        protected override Dictionary<string, string> GetTokenRequestParams()
        {
            return [];
        }
    }

    /// <summary>
    /// Seeds a known access token into the cache and asserts that
    /// <see cref="object.ToString"/> redacts it.
    /// </summary>
    [Fact]
    public void RedactsSecret()
    {
        var authenticator = new TestOAuthAuthenticator(new OpenId("https://example.com"));

        SeedCachedToken(authenticator, Secret);

        string rendered = authenticator.ToString();

        Assert.DoesNotContain(Secret, rendered);
        Assert.Contains("***", rendered);
    }

    /// <summary>
    /// Populates the private cached-token field of <paramref name="authenticator"/>
    /// with a non-expiring token carrying <paramref name="accessToken"/>, avoiding
    /// any network token exchange.
    /// </summary>
    private static void SeedCachedToken(OAuthAuthenticator authenticator, string accessToken)
    {
        Type tokenType =
            typeof(OAuthAuthenticator).GetNestedType("Token", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OAuthAuthenticator.Token type not found.");

        object token =
            Activator.CreateInstance(
                tokenType,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                binder: null,
                args: [accessToken, DateTime.UtcNow.AddHours(1)],
                culture: null
            ) ?? throw new InvalidOperationException("Unable to construct OAuthAuthenticator.Token.");

        FieldInfo field =
            typeof(OAuthAuthenticator).GetField("_token", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OAuthAuthenticator._token field not found.");

        field.SetValue(authenticator, token);
    }
}
