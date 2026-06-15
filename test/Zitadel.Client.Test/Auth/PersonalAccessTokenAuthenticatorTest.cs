// Zitadel SDK
// Verifies that the authenticator's string representation never leaks credentials.

using Xunit;
using Zitadel.Client.Auth;

namespace Test.Auth;

/// <summary>
/// Ensures <see cref="PersonalAccessTokenAuthenticator"/> masks its personal
/// access token (rendered as <c>***</c>) in <see cref="object.ToString"/>,
/// matching the redaction behaviour of the other Zitadel SDKs.
/// </summary>
public class PersonalAccessTokenAuthenticatorTest
{
    private const string Secret = "super-secret-value-should-not-appear";

    /// <summary>
    /// Constructs the authenticator with a known token and asserts that
    /// <see cref="object.ToString"/> redacts the token.
    /// </summary>
    [Fact]
    public void RedactsSecret()
    {
        var authenticator = new PersonalAccessTokenAuthenticator("https://example.com", Secret);

        string rendered = authenticator.ToString();

        Assert.DoesNotContain(Secret, rendered);
        Assert.Contains("***", rendered);
    }
}
