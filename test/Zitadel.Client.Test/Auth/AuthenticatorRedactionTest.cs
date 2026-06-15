// Zitadel SDK
// Verifies that authenticator string representations never leak credentials.

using System.Security.Cryptography;
using Xunit;
using Zitadel.Client.Auth;

namespace Test.Auth;

/// <summary>
/// Ensures every credential-bearing authenticator masks its sensitive value
/// (rendered as <c>***</c>) in <see cref="object.ToString"/>, matching the
/// redaction behaviour of the other Zitadel SDKs.
/// </summary>
public class AuthenticatorRedactionTest
{
    private const string Secret = "super-secret-value-should-not-appear";

    [Fact]
    public void BearerToStringOmitsToken()
    {
        var authenticator = new BearerAuthenticator("https://example.com", Secret);

        string rendered = authenticator.ToString();

        Assert.DoesNotContain(Secret, rendered);
        Assert.Contains("***", rendered);
    }

    [Fact]
    public void PersonalAccessTokenToStringOmitsToken()
    {
        var authenticator = new PersonalAccessTokenAuthenticator("https://example.com", Secret);

        string rendered = authenticator.ToString();

        Assert.DoesNotContain(Secret, rendered);
        Assert.Contains("***", rendered);
    }

    [Fact]
    public void ZitadelAccessTokenToStringOmitsToken()
    {
        var authenticator = new ZitadelAccessTokenAuthenticator("https://example.com", Secret);

        string rendered = authenticator.ToString();

        Assert.DoesNotContain(Secret, rendered);
        Assert.Contains("***", rendered);
    }

    [Fact]
    public void ClientCredentialsToStringOmitsSecretButShowsClientId()
    {
        var authenticator = ClientCredentialsAuthenticator
            .CreateBuilder("https://example.com", "my-client-id", Secret)
            .Build();

        string rendered = authenticator.ToString();

        Assert.DoesNotContain(Secret, rendered);
        Assert.Contains("***", rendered);
        Assert.Contains("my-client-id", rendered);
    }

    [Fact]
    public void WebTokenToStringOmitsKeyMaterial()
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
