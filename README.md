# Zitadel SDK for .NET

The official Zitadel SDK for .NET. It is a convenience wrapper around the
Zitadel APIs that helps you manage resources, settings, and configurations
in your Zitadel instance — users, organizations, projects, applications,
and more — directly from your .NET application.

> **Note:** This SDK is intended for **machine-to-machine** access to the
> Zitadel management APIs using a service account or personal access token.
> It is **not** a library for authenticating your end users (login, sessions,
> OIDC sign-in flows in your own app). For user-facing authentication, use
> Zitadel's hosted login or one of the OIDC/SAML client libraries for your
> framework.

## Installation

```bash
dotnet add package Zitadel.Client
```

## Usage

Create a client by pairing your Zitadel host with an authenticator, then
call any of the typed API service groups.

```csharp
using Zitadel.Client;
using Zitadel.Client.Auth;
using Zitadel.Client.Models;

// Authenticate with OAuth2 client credentials...
var authenticator = ClientCredentialsAuthenticator
    .CreateBuilder("https://my-instance.zitadel.cloud", "client-id", "client-secret")
    .Build();

// ...or with a personal access token...
// var authenticator = new PersonalAccessTokenAuthenticator(
//     "https://my-instance.zitadel.cloud", "my-token");

// ...or with a service-account key (JWT profile).
// var authenticator = WebTokenAuthenticator.FromJson(
//     "https://my-instance.zitadel.cloud", "service-account.json");

using var zitadel = Zitadel.WithAuthenticator(authenticator);

var response = await zitadel.UserService.GetUserByIDAsync(
    new UserServiceGetUserByIDRequest { UserId = "1234567890" });

Console.WriteLine(response.User?.UserId);
```

### Error handling

All errors thrown by the SDK derive from `ZitadelException`. API responses
that fail surface as `ApiException` (a subclass of `ZitadelException`),
carrying the HTTP status and response details.

```csharp
try
{
    var response = await zitadel.UserService.GetUserByIDAsync(
        new UserServiceGetUserByIDRequest { UserId = "unknown" });
}
catch (ApiException ex)
{
    Console.WriteLine($"Request failed: {ex.Message}");
}
```
