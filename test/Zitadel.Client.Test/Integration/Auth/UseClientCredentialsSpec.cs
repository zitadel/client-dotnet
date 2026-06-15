// Zitadel SDK
// SettingsService auth check via OAuth2 client credentials, ported from the other SDKs.

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Zitadel.Client.Test.Integration.Auth;

/// <summary>
/// SettingsService Integration Tests (Client Credentials).
///
/// <para>Verifies the general-settings endpoint when authenticating via the
/// OAuth2 client-credentials grant:</para>
/// <list type="number">
///   <item><description>Retrieve general settings successfully with valid credentials.</description></item>
///   <item><description>Raise an <see cref="ApiException"/> when using invalid credentials.</description></item>
/// </list>
///
/// <para>Valid credentials are minted on the fly by asking the Zitadel
/// management API for a fresh client secret for the seeded machine user,
/// mirroring the <c>generateUserSecret</c> helper in the other SDK suites.</para>
/// </summary>
[Collection(ZitadelStackCollection.Name)]
public sealed class UseClientCredentialsSpec
{
    private readonly ZitadelStackFixture _stack;

    public UseClientCredentialsSpec(ZitadelStackFixture stack)
    {
        _stack = stack;
    }

    [Fact]
    public async Task RetrievesGeneralSettingsWithValidClientCredentials()
    {
        (string clientId, string clientSecret) = await GenerateUserSecretAsync(
            _stack.AuthToken,
            "api-user"
        );

        using var client = ZitadelClients.WithClientCredentials(
            _stack.BaseUrl,
            clientId,
            clientSecret
        );

        Assert.NotNull(await client.SettingsService.GetGeneralSettingsAsync(new object()));
    }

    [Fact]
    public async Task RaisesApiExceptionWithInvalidClientCredentials()
    {
        using var client = ZitadelClients.WithClientCredentials(
            _stack.BaseUrl,
            "invalid",
            "invalid"
        );

        _ = await Assert.ThrowsAnyAsync<ApiException>(() =>
            client.SettingsService.GetGeneralSettingsAsync(new object())
        );
    }

    private static async Task<(string ClientId, string ClientSecret)> GenerateUserSecretAsync(
        string token,
        string loginName
    )
    {
        using HttpClient http = new();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json")
        );

        string lookupUrl =
            "http://localhost:18104/management/v1/global/users/_by_login_name?loginName="
            + Uri.EscapeDataString(loginName);
        using HttpResponseMessage lookup = await http.GetAsync(new Uri(lookupUrl));
        string lookupBody = await lookup.Content.ReadAsStringAsync();
        if (!lookup.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"API call to retrieve user failed for login name '{loginName}'. Response: {lookupBody}"
            );
        }

        string userId =
            ReadNestedString(lookupBody, "user", "id")
            ?? throw new InvalidOperationException(
                $"Could not parse a valid user ID for login name '{loginName}'. Response: {lookupBody}"
            );

        using HttpRequestMessage secretRequest = new(
            HttpMethod.Put,
            new Uri($"http://localhost:18104/management/v1/users/{userId}/secret")
        );
        secretRequest.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        using HttpResponseMessage secret = await http.SendAsync(secretRequest);
        string secretBody = await secret.Content.ReadAsStringAsync();
        if (!secret.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"API call to generate secret failed for user ID '{userId}'. Response: {secretBody}"
            );
        }

        using JsonDocument doc = JsonDocument.Parse(secretBody);
        string? clientId = doc.RootElement.TryGetProperty("clientId", out JsonElement idElement)
            ? idElement.GetString()
            : null;
        string? clientSecret = doc.RootElement.TryGetProperty(
            "clientSecret",
            out JsonElement secretElement
        )
            ? secretElement.GetString()
            : null;

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            throw new InvalidOperationException(
                $"API response for secret is missing 'clientId' or 'clientSecret'. Response: {secretBody}"
            );
        }

        return (clientId, clientSecret);
    }

    private static string? ReadNestedString(string json, string outer, string inner)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        if (
            doc.RootElement.TryGetProperty(outer, out JsonElement outerElement)
            && outerElement.ValueKind == JsonValueKind.Object
            && outerElement.TryGetProperty(inner, out JsonElement innerElement)
        )
        {
            return innerElement.GetString();
        }
        return null;
    }
}
