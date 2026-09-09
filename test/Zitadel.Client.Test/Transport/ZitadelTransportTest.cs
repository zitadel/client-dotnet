// Zitadel SDK
// Testcontainers-based transport tests, ported from the other Zitadel SDKs.
//
// A WireMock container stubs the OAuth discovery, token, and GetGeneralSettings
// endpoints over both HTTP and HTTPS (with a self-signed cert chained to the
// fixture CA), and a Squid container provides a forward proxy. The tests assert
// that TransportOptions correctly drive TLS verification, custom CA trust,
// default headers, and proxy routing.

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Zitadel.Client.Auth;
using ZitadelClient = Zitadel.Client.Zitadel;

namespace Zitadel.Client.Test.Transport;

/// <summary>
/// Transport-layer integration tests exercising <see cref="TransportOptions"/>
/// against a WireMock stub and a Squid forward proxy.
///
/// <para>The WireMock stub returns the request scheme as
/// <c>defaultLanguage</c> and echoes the <c>X-Custom-Header</c> value as
/// <c>defaultOrgId</c>, so each test can assert that the request reached the
/// stub over the expected transport.</para>
/// </summary>
public sealed class ZitadelTransportTest : IAsyncLifetime
{
    private static string FixturesDir => Path.Combine(AppContext.BaseDirectory, "fixtures");

    private INetwork _network = null!;
    private IContainer _wiremock = null!;
    private IContainer _proxy = null!;

    private static string CaCertPath => Path.Combine(FixturesDir, "ca.pem");

    private string Host => _wiremock.Hostname;
    private ushort HttpPort => _wiremock.GetMappedPublicPort(8080);
    private ushort HttpsPort => _wiremock.GetMappedPublicPort(8443);
    private ushort ProxyPort => _proxy.GetMappedPublicPort(3128);

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        _network = new NetworkBuilder().Build();

        _wiremock = new ContainerBuilder("wiremock/wiremock:3.12.1")
            .WithNetwork(_network)
            .WithNetworkAliases("wiremock")
            .WithPortBinding(8080, true)
            .WithPortBinding(8443, true)
            .WithResourceMapping(
                new FileInfo(Path.Combine(FixturesDir, "keystore.p12")),
                new FileInfo("/home/wiremock/keystore.p12")
            )
            .WithResourceMapping(
                new DirectoryInfo(Path.Combine(FixturesDir, "mappings")),
                "/home/wiremock/mappings"
            )
            .WithCommand(
                "--https-port",
                "8443",
                "--https-keystore",
                "/home/wiremock/keystore.p12",
                "--keystore-password",
                "password",
                "--keystore-type",
                "PKCS12",
                "--global-response-templating"
            )
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(r => r.ForPort(8080).ForPath("/__admin/mappings"))
            )
            .Build();

        _proxy = new ContainerBuilder("ubuntu/squid:6.10-24.10_beta")
            .WithNetwork(_network)
            .WithPortBinding(3128, true)
            .WithResourceMapping(
                new FileInfo(Path.Combine(FixturesDir, "squid.conf")),
                new FileInfo("/etc/squid/squid.conf")
            )
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(3128))
            .Build();

        await _wiremock.StartAsync();
        await _proxy.StartAsync();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _proxy.DisposeAsync();
        await _wiremock.DisposeAsync();
        await _network.DisposeAsync();
    }

    [Fact]
    public async Task CustomCaCertIsTrusted()
    {
        TransportOptions transport = TransportOptions.Builder().CaCertPath(CaCertPath).Build();
        using var client = ZitadelClient.WithAuthenticator(
            ClientCredentialsAuthenticator
                .CreateBuilder($"https://{Host}:{HttpsPort}", "dummy-client", "dummy-secret")
                .Build(),
            transport
        );

        Models.SettingsServiceGetGeneralSettingsResponse response =
            await client.SettingsService.GetGeneralSettingsAsync(new object());

        Assert.Equal("https", response.DefaultLanguage);
    }

    [Fact]
    public async Task InsecureModeSkipsVerification()
    {
        TransportOptions transport = TransportOptions.Builder().VerifySsl(false).Build();
        using var client = ZitadelClient.WithAuthenticator(
            ClientCredentialsAuthenticator
                .CreateBuilder($"https://{Host}:{HttpsPort}", "dummy-client", "dummy-secret")
                .Build(),
            transport
        );

        Models.SettingsServiceGetGeneralSettingsResponse response =
            await client.SettingsService.GetGeneralSettingsAsync(new object());

        Assert.Equal("https", response.DefaultLanguage);
    }

    [Fact]
    public async Task DefaultHeadersAreSent()
    {
        TransportOptions transport = TransportOptions
            .Builder()
            .DefaultHeader("X-Custom-Header", "test-value")
            .Build();
        using var client = ZitadelClient.WithAuthenticator(
            ClientCredentialsAuthenticator
                .CreateBuilder($"http://{Host}:{HttpPort}", "dummy-client", "dummy-secret")
                .Build(),
            transport
        );

        Models.SettingsServiceGetGeneralSettingsResponse response =
            await client.SettingsService.GetGeneralSettingsAsync(new object());

        Assert.Equal("http", response.DefaultLanguage);
        Assert.Equal("test-value", response.DefaultOrgId);
    }

    [Fact]
    public async Task ProxyRoutesRequest()
    {
        TransportOptions transport = TransportOptions
            .Builder()
            .Proxy($"http://{Host}:{ProxyPort}")
            .Build();
        using var client = ZitadelClient.WithAuthenticator(
            new BearerAuthenticator("http://wiremock:8080", "test-token"),
            transport
        );

        Models.SettingsServiceGetGeneralSettingsResponse response =
            await client.SettingsService.GetGeneralSettingsAsync(new object());

        Assert.Equal("http", response.DefaultLanguage);
    }

    [Fact]
    public async Task MissingCaCertFails()
    {
        using var client = ZitadelClient.WithAuthenticator(
            ClientCredentialsAuthenticator
                .CreateBuilder($"https://{Host}:{HttpsPort}", "dummy-client", "dummy-secret")
                .Build()
        );

        _ = await Assert.ThrowsAnyAsync<Exception>(() =>
            client.SettingsService.GetGeneralSettingsAsync(new object())
        );
    }
}
