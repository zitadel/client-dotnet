// Zitadel SDK
// Test helpers that build a Client for each supported authentication method.
//
// The generated facade only ships the WithToken static factory; the other two
// schemes are constructed from their bespoke authenticators, exactly as an SDK
// consumer would. Keeping the construction in one place mirrors the
// Zitadel.withAccessToken / withClientCredentials / withPrivateKey entry points
// the other SDKs expose.

using Zitadel.Client.Auth;

namespace Zitadel.Client.Test.Integration;

/// <summary>
/// Factory helpers mirroring the <c>withAccessToken</c> /
/// <c>withClientCredentials</c> / <c>withPrivateKey</c> entry points the other
/// Zitadel SDKs expose, built on top of the generated .NET authenticators.
/// </summary>
internal static class ZitadelClients
{
    /// <summary>
    /// Builds a client authenticated with a personal access token.
    /// </summary>
    public static Client WithAccessToken(string host, string token)
    {
        return new Client(new PersonalAccessTokenAuthenticator(host, token));
    }

    /// <summary>
    /// Builds a client authenticated with the OAuth2 client-credentials grant.
    /// </summary>
    public static Client WithClientCredentials(string host, string clientId, string clientSecret)
    {
        return new Client(
            ClientCredentialsAuthenticator.CreateBuilder(host, clientId, clientSecret).Build()
        );
    }

    /// <summary>
    /// Builds a client authenticated with a private-key (JWT bearer) assertion
    /// loaded from a service-account JSON key file.
    /// </summary>
    public static Client WithPrivateKey(string host, string jsonKeyPath)
    {
        return new Client(WebTokenAuthenticator.FromJson(host, jsonKeyPath));
    }
}
