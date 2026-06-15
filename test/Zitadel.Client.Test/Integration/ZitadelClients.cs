// Zitadel SDK
// Test helpers that build a Zitadel facade for each supported authentication
// method.
//
// The generated facade ships the WithToken static factory plus the generic
// WithAuthenticator entry point; the bespoke schemes are constructed from their
// authenticators and handed to WithAuthenticator, exactly as an SDK consumer
// would. Keeping the construction in one place mirrors the
// Zitadel.withAccessToken / withClientCredentials / withPrivateKey entry points
// the other SDKs expose.

using Zitadel.Client.Auth;
using ZitadelClient = Zitadel.Client.Zitadel;

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
    public static ZitadelClient WithAccessToken(string host, string token)
    {
        return ZitadelClient.WithAuthenticator(new PersonalAccessTokenAuthenticator(host, token));
    }

    /// <summary>
    /// Builds a client authenticated with the OAuth2 client-credentials grant.
    /// </summary>
    public static ZitadelClient WithClientCredentials(
        string host,
        string clientId,
        string clientSecret
    )
    {
        return ZitadelClient.WithAuthenticator(
            ClientCredentialsAuthenticator.CreateBuilder(host, clientId, clientSecret).Build()
        );
    }

    /// <summary>
    /// Builds a client authenticated with a private-key (JWT bearer) assertion
    /// loaded from a service-account JSON key file.
    /// </summary>
    public static ZitadelClient WithPrivateKey(string host, string jsonKeyPath)
    {
        return ZitadelClient.WithAuthenticator(WebTokenAuthenticator.FromJson(host, jsonKeyPath));
    }
}
