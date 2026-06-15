// Zitadel SDK
// Verifies that the authenticator's string representation never leaks credentials.

using Xunit;
using Zitadel.Client.Auth;

namespace Test.Auth;

/// <summary>
/// Ensures <see cref="ClientCredentialsAuthenticator"/> masks its client secret
/// (rendered as <c>***</c>) in <see cref="object.ToString"/> while keeping the
/// non-sensitive client id visible, matching the redaction behaviour of the
/// other Zitadel SDKs.
/// </summary>
public class ClientCredentialsAuthenticatorTest
{
    private const string Secret = "super-secret-value-should-not-appear";

    /// <summary>
    /// Constructs the authenticator with a known client secret and asserts that
    /// <see cref="object.ToString"/> redacts the secret while still exposing the
    /// client id.
    /// </summary>
    [Fact]
    public void RedactsSecret()
    {
        var authenticator = ClientCredentialsAuthenticator
            .CreateBuilder("https://example.com", "my-client-id", Secret)
            .Build();

        string rendered = authenticator.ToString();

        Assert.DoesNotContain(Secret, rendered);
        Assert.Contains("***", rendered);
        Assert.Contains("my-client-id", rendered);
    }
}
