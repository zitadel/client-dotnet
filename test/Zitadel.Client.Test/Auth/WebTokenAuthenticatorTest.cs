// Zitadel SDK
// Verifies the web token authenticator's redaction, key-file loading and
// argument validation.

using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;
using Zitadel.Client.Auth;

namespace Test.Auth;

/// <summary>
/// Verifies that <see cref="WebTokenAuthenticator"/> redacts its cached token,
/// loads Zitadel key files, and rejects unusable key files and builder
/// arguments with <see cref="ArgumentException"/>. Construction never touches
/// the network.
/// </summary>
public sealed class WebTokenAuthenticatorTest : IDisposable
{
    private const string Host = "https://example.zitadel.cloud";

    private readonly List<string> _keyFiles = [];

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (string path in _keyFiles)
        {
            File.Delete(path);
        }
    }

    private string KeyFile(string content)
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        _keyFiles.Add(path);
        return path;
    }

    /// <summary>
    /// Seeds a cached token and asserts that <see cref="object.ToString"/>
    /// redacts it and never renders the key material.
    /// </summary>
    [Fact]
    public void RedactsSecret()
    {
        using RSA key = RSA.Create(2048);
        string pem = key.ExportRSAPrivateKeyPem();
        var authenticator = WebTokenAuthenticator
            .CreateBuilder(Host, "user-id", key)
            .KeyId("key-id")
            .Build();
        FieldInfo field =
            typeof(OAuthAuthenticator).GetField(
                "_accessToken",
                BindingFlags.Instance | BindingFlags.NonPublic
            ) ?? throw new InvalidOperationException("OAuthAuthenticator._accessToken not found.");
        field.SetValue(authenticator, "minted-web-token-do-not-leak");

        string rendered = authenticator.ToString();

        Assert.DoesNotContain(pem, rendered);
        Assert.DoesNotContain("minted-web-token-do-not-leak", rendered);
        Assert.Contains("***", rendered);
    }

    [Fact]
    public void LoadsKeyFile()
    {
        using RSA key = RSA.Create(2048);
        string path = KeyFile(
            JsonSerializer.Serialize(
                new Dictionary<string, string>
                {
                    ["type"] = "serviceaccount",
                    ["keyId"] = "key-1",
                    ["userId"] = "user-1",
                    ["key"] = key.ExportRSAPrivateKeyPem(),
                }
            )
        );

        Assert.Equal(Host, WebTokenAuthenticator.FromJson(Host, path).GetHost());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"userId\":\"user-1\",\"keyId\":\"key-1\"}")]
    [InlineData("{\"userId\":\"user-1\",\"keyId\":\"key-1\",\"key\":\"not a pem\"}")]
    public void RejectsMalformedKeyFile(string content)
    {
        string path = KeyFile(content);

        Assert.Throws<ArgumentException>(() => WebTokenAuthenticator.FromJson(Host, path));
    }

    [Fact]
    public void RejectsMissingKeyFile()
    {
        string path = Path.Combine(Path.GetTempPath(), "absent-zitadel-key.json");

        Assert.Throws<ArgumentException>(() => WebTokenAuthenticator.FromJson(Host, path));
    }

    [Fact]
    public void RejectsBadBuilderArguments()
    {
        using RSA key = RSA.Create(2048);
        using RSA publicOnly = RSA.Create();
        publicOnly.ImportRSAPublicKey(key.ExportRSAPublicKey(), out _);
        WebTokenAuthenticatorBuilder builder = WebTokenAuthenticator.CreateBuilder(
            Host,
            "user-1",
            key
        );

        Assert.Throws<ArgumentException>(() => WebTokenAuthenticator.CreateBuilder(Host, "", key));
        Assert.Throws<ArgumentException>(() =>
            WebTokenAuthenticator.CreateBuilder(Host, "user-1", publicOnly)
        );
        Assert.Throws<ArgumentException>(() => builder.JwtAlgorithm("HS256"));
        Assert.Throws<ArgumentException>(() => builder.TokenLifetimeSeconds(0));
        Assert.Throws<ArgumentException>(() => builder.KeyId(""));
    }
}
