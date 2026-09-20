# Zitadel SDK - AI Agent Reference

## Installation

```bash
dotnet add package Zitadel.Client
```

## Quick Start

```csharp
using Zitadel.Client;
using Zitadel.Client.Auth;

var client = global::Zitadel.Client.Zitadel.WithToken("https://api.example.com", "your-token");
```

## Authentication

All authentication is handled via `IAuthenticator` implementations passed to the client constructor.

### Bearer Token

```csharp
using Zitadel.Client.Auth;

var authenticator = new BearerAuthenticator("https://api.example.com", "your-token");
var client = new global::Zitadel.Client.Zitadel(authenticator);
```

## Servers

If the OpenAPI spec defines multiple servers, the generated `Servers` class exposes each as a `ServerConfiguration` field (e.g., `Servers.Server0`, `Servers.Server1`, ...) plus a `Servers.All` collection. Pass the desired server's URL to the client:

```csharp
using Zitadel.Client;

var client = global::Zitadel.Client.Zitadel.WithToken(Servers.Server0.GetUrl(), "your-token");
```

## Testing

The `IAuthenticator` interface is the seam for tests: substitute a fake authenticator that returns a known header map, and assert your code calls the API the way you expect.

```csharp
using Zitadel.Client.Auth;

public sealed class FakeAuthenticator : IAuthenticator
{
    public string GetHost() => "https://api.example.com";

    public Dictionary<string, string> GetAuthHeaders() =>
        new() { ["Authorization"] = "Bearer test-token" };
}

var client = new global::Zitadel.Client.Zitadel(new FakeAuthenticator());
```

## Error Handling

All API errors derive from `ApiException`. The error hierarchy is:

- `ApiException` (base)
  - `ClientException` (4xx)
    - `BadRequestException` (400)
    - `UnauthorizedException` (401)
    - `ForbiddenException` (403)
    - `NotFoundException` (404)
    - `ConflictException` (409)
    - `UnprocessableEntityException` (422)
  - `ServerException` (5xx)
    - `InternalServerErrorException` (500)

```csharp
using Zitadel.Client;
using Zitadel.Client.Errors;

try
{
    var result = await client.ActionService.ActivatePublicKeyAsync(/* parameters */);
}
catch (NotFoundException e)
{
    Console.WriteLine($"Not found: {e.Message}");
}
catch (ClientException e)
{
    Console.WriteLine($"Client error {e.StatusCode}: {e.Message}");
}
catch (ServerException e)
{
    Console.WriteLine($"Server error: {e.Message}");
}
catch (ApiException e)
{
    Console.WriteLine($"API error: {e.Message}");
}
```

## Configuration

### Custom Transport Options

```csharp
var transport = TransportOptions.Builder()
    .Proxy("http://proxy:3128")
    .Timeout(5000)
    .Build();

var client = new global::Zitadel.Client.Zitadel(authenticator, transport);
```

The client implements `IDisposable`. Use `using` statements or call `Dispose()` when done.

## API Methods

Each API group is exposed as a typed property on the client (e.g., `client.ActionService`). API classes have methods that correspond to OpenAPI operations, accepting typed request parameters and returning typed response models.

All API methods are asynchronous; invoke them with `await`.

## Models

Models are generated as C# classes under the `Zitadel.Client.Models` namespace.

```csharp
using Zitadel.Client.Models;

var model = new ActionServiceActivatePublicKeyRequest();
```

## Binary / File Uploads

File upload parameters are typed as `Stream`. Binary response bodies are returned as `byte[]`.

## Comment Style

Never place a comment on the same line as code. Use block comments (`/* ... */`); XML doc comments (`///`) are fine.

```good
/* This explains the logic */
var x = 1;
```

```bad
// This explains the logic
var x = 1;
```
