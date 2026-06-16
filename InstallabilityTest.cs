using Zitadel.Client.Auth;

namespace Zitadel.Client.Sanity;

/// <summary>
/// Smoke test proving the packaged SDK is consumable from a downstream
/// project: it restores from the local feed and exposes its public API.
/// Constructing a public authenticator is enough to link against the
/// packed assembly and confirm the published type surface resolves.
/// </summary>
public class InstallabilityTest
{
    [Fact]
    public void PublicAuthenticatorIsConstructable()
    {
        var authenticator = new PersonalAccessTokenAuthenticator(
            "https://example.zitadel.cloud",
            "tkn");

        Assert.NotNull(authenticator);
    }
}
