// Zitadel SDK
// SettingsService auth check via private-key assertion, ported from the other SDKs.

using System.Security.Cryptography;
using Zitadel.Client.Auth;
using Zitadel.Client.Errors;
using ZitadelClient = Zitadel.Client.Zitadel;

namespace Zitadel.Client.Test.Integration.Auth;

/// <summary>
/// SettingsService Integration Tests (Private Key Assertion).
///
/// <para>Verifies the general-settings endpoint when authenticating via a
/// private-key (JWT bearer) assertion:</para>
/// <list type="number">
///   <item><description>Retrieve general settings successfully with a valid private key.</description></item>
///   <item><description>Raise an <see cref="OAuth2ServerException"/> when signing with a key the instance does not know.</description></item>
/// </list>
///
/// <para>Each test instantiates a new client to ensure a clean, stateless call.</para>
/// </summary>
[Collection(ZitadelStackCollection.Name)]
public sealed class UsePrivateKeySpec
{
    private readonly ZitadelStackFixture _stack;

    public UsePrivateKeySpec(ZitadelStackFixture stack)
    {
        _stack = stack;
    }

    [Fact]
    public async Task RetrievesGeneralSettingsWithValidPrivateKey()
    {
        using var client = ZitadelClients.WithPrivateKey(_stack.BaseUrl, _stack.JwtKeyPath);

        Assert.NotNull(await client.SettingsService.GetGeneralSettingsAsync(new object()));
    }

    [Fact]
    public async Task RaisesApiExceptionWithInvalidPrivateKey()
    {
        using RSA key = RSA.Create(2048);
        using var client = ZitadelClient.WithAuthenticator(
            WebTokenAuthenticator.CreateBuilder(_stack.BaseUrl, "invalid", key).KeyId("invalid").Build()
        );

        _ = await Assert.ThrowsAsync<OAuth2ServerException>(() =>
            client.SettingsService.GetGeneralSettingsAsync(new object())
        );
    }
}
