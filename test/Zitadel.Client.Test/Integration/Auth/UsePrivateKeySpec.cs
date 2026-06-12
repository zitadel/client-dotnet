// Zitadel SDK
// SettingsService auth check via private-key assertion, ported from the other SDKs.

namespace Zitadel.Client.Test.Integration.Auth;

/// <summary>
/// SettingsService Integration Tests (Private Key Assertion).
///
/// <para>Verifies the general-settings endpoint when authenticating via a
/// private-key (JWT bearer) assertion:</para>
/// <list type="number">
///   <item><description>Retrieve general settings successfully with a valid private key.</description></item>
///   <item><description>Raise an <see cref="ApiException"/> when the key is presented to a host that rejects it.</description></item>
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
        using Client client = ZitadelClients.WithPrivateKey(_stack.BaseUrl, _stack.JwtKeyPath);

        Assert.NotNull(await client.SettingsService.GetGeneralSettingsAsync(new object()));
    }

    [Fact]
    public async Task RaisesApiExceptionWithInvalidPrivateKey()
    {
        using Client client = ZitadelClients.WithPrivateKey(
            "https://zitadel.cloud",
            _stack.JwtKeyPath
        );

        _ = await Assert.ThrowsAnyAsync<ApiException>(() =>
            client.SettingsService.GetGeneralSettingsAsync(new object())
        );
    }
}
