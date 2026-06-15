// Zitadel SDK
// Verifies that the authenticator's string representation never leaks credentials.

using System.Security.Cryptography;
using Xunit;
using Zitadel.Client.Auth;

namespace Test.Auth;

/// <summary>
/// Ensures <see cref="WebTokenAuthenticator"/> masks its private signing key
/// (rendered as <c>***</c>) in <see cref="object.ToString"/>, matching the
/// redaction behaviour of the other Zitadel SDKs.
/// </summary>
public class WebTokenAuthenticatorTest
{
    /// <summary>
    /// Constructs the authenticator with a known RSA private key and asserts
    /// that <see cref="object.ToString"/> redacts the key material.
    /// </summary>
    [Fact]
    public void RedactsSecret()
    {
        using RSA key = RSA.Create(2048);
        string pem = key.ExportRSAPrivateKeyPem();

        var authenticator = WebTokenAuthenticator
            .CreateBuilder("https://example.com", "user-id", key)
            .KeyId("key-id")
            .Build();

        string rendered = authenticator.ToString();

        Assert.DoesNotContain(pem, rendered);
        Assert.DoesNotContain("BEGIN RSA PRIVATE KEY", rendered);
        Assert.Contains("***", rendered);
    }
}
