// Zitadel SDK
// Bespoke integration-test fixture: provisions a full Zitadel stack via the
// docker-compose file under etc/ and exposes the credentials it mints.

using System.Diagnostics;

namespace Zitadel.Client.Test.Integration;

/// <summary>
/// Brings a Docker Compose Zitadel stack up before the integration tests run
/// and tears it down afterwards. Mirrors the docker-compose provisioning
/// fixture used by the other Zitadel SDKs (Python's <c>docker_compose</c>
/// fixture, Java's <c>AbstractIntegrationTest</c>).
///
/// <para>The stack writes a personal access token to
/// <c>etc/zitadel_output/pat.txt</c> and a service-account key to
/// <c>etc/zitadel_output/sa-key.json</c>; both are surfaced here.</para>
///
/// <para>The fixture is shared across the integration test classes through
/// <see cref="ZitadelStackCollection"/> so the (slow) stack starts only once
/// per test run.</para>
/// </summary>
public sealed class ZitadelStackFixture : IAsyncLifetime
{
    /// <summary>The base URL the provisioned Zitadel instance is reachable at.</summary>
    public string BaseUrl { get; } = "http://localhost:18104";

    /// <summary>The personal access token minted by the stack.</summary>
    public string AuthToken { get; private set; } = string.Empty;

    /// <summary>The absolute path to the service-account JSON key minted by the stack.</summary>
    public string JwtKeyPath { get; private set; } = string.Empty;

    private static string ComposeFilePath =>
        Path.Combine(RepositoryRoot, "etc", "docker-compose.yaml");

    private static string ComposeFileDir => Path.GetDirectoryName(ComposeFilePath)!;

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        await RunComposeAsync(
            ["up", "--detach", "--no-color", "--quiet-pull", "--yes"],
            TimeSpan.FromMinutes(5)
        );

        string authTokenPath = Path.Combine(ComposeFileDir, "zitadel_output", "pat.txt");
        if (!File.Exists(authTokenPath))
        {
            throw new InvalidOperationException($"Auth token file not found at: {authTokenPath}");
        }
        AuthToken = (await File.ReadAllTextAsync(authTokenPath)).Trim();

        string jwtKeyPath = Path.Combine(ComposeFileDir, "zitadel_output", "sa-key.json");
        if (!File.Exists(jwtKeyPath))
        {
            throw new InvalidOperationException($"JWT key file not found at path: {jwtKeyPath}");
        }
        JwtKeyPath = jwtKeyPath;

        // Give Zitadel a moment to finish initialising its projections, matching
        // the fixed settle delay used by the other SDKs' fixtures.
        await Task.Delay(TimeSpan.FromSeconds(20));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await RunComposeAsync(["down", "-v"], TimeSpan.FromMinutes(5));
        }
        catch (Exception)
        {
            // Teardown failures must not mask test results.
        }
    }

    private static async Task RunComposeAsync(string[] composeArgs, TimeSpan timeout)
    {
        ProcessStartInfo psi = new()
        {
            FileName = "docker",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("compose");
        psi.ArgumentList.Add("--file");
        psi.ArgumentList.Add(ComposeFilePath);
        foreach (string arg in composeArgs)
        {
            psi.ArgumentList.Add(arg);
        }

        using Process process =
            Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start the docker compose process.");

        Task<string> stdOut = process.StandardOutput.ReadToEndAsync();
        Task<string> stdErr = process.StandardError.ReadToEndAsync();

        using CancellationTokenSource cts = new(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException(
                $"docker compose {string.Join(' ', composeArgs)} timed out after {timeout}."
            );
        }

        if (process.ExitCode != 0)
        {
            string output = await stdOut;
            string error = await stdErr;
            throw new InvalidOperationException(
                $"docker compose {string.Join(' ', composeArgs)} failed "
                    + $"(exit code {process.ExitCode}).\n{output}\n{error}"
            );
        }
    }

    private static string RepositoryRoot
    {
        get
        {
            // Walk up from the test assembly location until the etc/docker-compose.yaml
            // marker is found, so the fixture works regardless of the build output depth.
            DirectoryInfo? dir = new(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "etc", "docker-compose.yaml")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            throw new InvalidOperationException(
                "Could not locate the repository root (etc/docker-compose.yaml) "
                    + $"starting from {AppContext.BaseDirectory}."
            );
        }
    }
}

/// <summary>
/// xUnit collection that shares a single <see cref="ZitadelStackFixture"/> across
/// every integration test class, so the Docker Compose stack is provisioned once.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ZitadelStackCollection : ICollectionFixture<ZitadelStackFixture>
{
    /// <summary>The collection name shared by the integration test classes.</summary>
    public const string Name = "Zitadel docker-compose stack";
}
