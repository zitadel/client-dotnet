using Zitadel.Client;
using Zitadel.Client.Auth;

namespace Zitadel.Client.Test;

/// <summary>
/// Non-Docker smoke test verifying the SDK facade can be constructed and
/// disposed with each authenticator without touching the network. Keeps the
/// suite from being entirely Docker-gated.
/// </summary>
public class ClientSmokeTest
{
    [Fact]
    public void ConstructsClientWithBearerAuthenticator()
    {
        using Client client = Client.WithToken("http://localhost:18104", "test-token");

        Assert.NotNull(client.UserService);
        Assert.NotNull(client.SessionService);
        Assert.NotNull(client.SettingsService);
    }

    [Fact]
    public void ConstructsClientWithClientCredentialsAuthenticator()
    {
        using Client client = new(
            ClientCredentialsAuthenticator
                .CreateBuilder("http://localhost:18104", "client-id", "client-secret")
                .Build()
        );

        Assert.NotNull(client.SettingsService);
    }
}
