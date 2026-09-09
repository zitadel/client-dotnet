// Zitadel SDK
// SettingsService auth check via personal access token, ported from the other SDKs.

namespace Zitadel.Client.Test.Integration.Auth;

/// <summary>
/// SettingsService Integration Tests (Personal Access Token).
///
/// <para>Verifies the general-settings endpoint when authenticating via a
/// personal access token:</para>
/// <list type="number">
///   <item><description>Retrieve general settings successfully with a valid token.</description></item>
///   <item><description>Raise an <see cref="ApiException"/> when using an invalid token.</description></item>
/// </list>
///
/// <para>Each test instantiates a new client to ensure a clean, stateless call.</para>
/// </summary>
[Collection(ZitadelStackCollection.Name)]
public sealed class UseAccessTokenSpec
{
    private readonly ZitadelStackFixture _stack;

    public UseAccessTokenSpec(ZitadelStackFixture stack)
    {
        _stack = stack;
    }

    [Fact]
    public async Task RetrievesGeneralSettingsWithValidToken()
    {
        using var client = ZitadelClients.WithAccessToken(_stack.BaseUrl, _stack.AuthToken);

        Assert.NotNull(await client.SettingsService.GetGeneralSettingsAsync(new object()));
    }

    [Fact]
    public async Task RaisesApiExceptionWithInvalidToken()
    {
        using var client = ZitadelClients.WithAccessToken(_stack.BaseUrl, "invalid");

        _ = await Assert.ThrowsAnyAsync<ApiException>(() =>
            client.SettingsService.GetGeneralSettingsAsync(new object())
        );
    }
}
