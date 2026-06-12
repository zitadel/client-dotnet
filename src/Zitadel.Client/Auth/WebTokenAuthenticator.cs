// Zitadel SDK
// Bespoke authenticator aligned with the generated IHttpAwareAuthenticator interface.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Zitadel.Client.Auth;

/// <summary>
/// JWT-based Authenticator using the JWT Bearer Grant (RFC 7523).
/// <para>This class creates a signed JWT assertion and exchanges it for an
/// access token via the shared <see cref="IApiClient"/> inherited from
/// <see cref="OAuthAuthenticator"/>.</para>
/// </summary>
public class WebTokenAuthenticator : OAuthAuthenticator
{
    private const string GrantType = "urn:ietf:params:oauth:grant-type:jwt-bearer";

    private readonly string _jwtIssuer;
    private readonly string _jwtSubject;
    private readonly string _jwtAudience;
    private readonly RSA _keySigner;
    private readonly TimeSpan _tokenLifetime;
    private readonly string? _keyId;

    internal WebTokenAuthenticator(
        OpenId openId,
        string jwtIssuer,
        string jwtSubject,
        string jwtAudience,
        RSA keySigner,
        TimeSpan tokenLifetime,
        string? keyId,
        string? scope
    )
        : base(openId, scope)
    {
        _jwtIssuer = jwtIssuer;
        _jwtSubject = jwtSubject;
        _jwtAudience = jwtAudience;
        _keySigner = keySigner;
        _tokenLifetime = tokenLifetime;
        _keyId = keyId;
    }

    /// <summary>
    /// Creates a <see cref="WebTokenAuthenticator"/> from a JSON service-account file.
    /// </summary>
    public static WebTokenAuthenticator FromJson(string host, string jsonPath)
    {
        try
        {
            using FileStream fis = new(jsonPath, FileMode.Open, FileAccess.Read);
            return FromJson(host, fis);
        }
        catch (IOException e)
        {
            throw new ApiException($"Unable to read JSON file at {jsonPath}: {e.Message}", e);
        }
    }

    /// <summary>
    /// Creates a <see cref="WebTokenAuthenticator"/> from a JSON service-account stream.
    /// <para>The JSON must contain <c>userId</c>, <c>keyId</c>, and a PEM-encoded
    /// <c>key</c>.</para>
    /// </summary>
    public static WebTokenAuthenticator FromJson(string host, Stream inputStream)
    {
        Dictionary<string, JsonElement>? config;
        try
        {
            config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(inputStream);
        }
        catch (JsonException e)
        {
            throw new ApiException(
                $"Unable to read or parse JSON from input stream: {e.Message}",
                e
            );
        }

        if (config == null || config.Count == 0)
        {
            throw new ApiException("Expected a JSON object in input stream");
        }

        string? GetString(string k)
        {
            return config.TryGetValue(k, out JsonElement value) ? value.GetString() : null;
        }

        string? userId = GetString("userId");
        string? keyString = GetString("key");
        string? keyId = GetString("keyId");

        if (userId == null || keyString == null || keyId == null)
        {
            throw new ApiException("Missing required keys 'userId', 'keyId' or 'key' in JSON.");
        }

        RSA privateKey;
        try
        {
            privateKey = RSA.Create();
            privateKey.ImportFromPem(keyString);
        }
        catch (Exception e)
        {
            throw new ApiException($"Unable to convert key string to PrivateKey: {e.Message}", e);
        }

        return CreateBuilder(host, userId, privateKey).KeyId(keyId).Build();
    }

    /// <summary>
    /// Returns a new builder instance for WebTokenAuthenticator.
    /// </summary>
    public static WebTokenAuthenticatorBuilder CreateBuilder(
        string host,
        string userId,
        RSA privateKey
    )
    {
        return new WebTokenAuthenticatorBuilder(host, userId, userId, host, privateKey);
    }

    /// <inheritdoc/>
    protected override string GetGrantType()
    {
        return GrantType;
    }

    /// <inheritdoc/>
    protected override Dictionary<string, string> GetTokenRequestParams()
    {
        return new() { ["assertion"] = BuildAssertion() };
    }

    private string BuildAssertion()
    {
        try
        {
            long iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long exp = DateTimeOffset.UtcNow.Add(_tokenLifetime).ToUnixTimeSeconds();

            Dictionary<string, string> header = new() { ["alg"] = "RS256" };
            if (_keyId != null)
            {
                header["kid"] = _keyId;
            }

            Dictionary<string, object> payload = new()
            {
                ["iss"] = _jwtIssuer,
                ["sub"] = _jwtSubject,
                ["aud"] = _jwtAudience,
                ["iat"] = iat,
                ["exp"] = exp,
            };

            string encodedHeader = Base64Url(
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(header))
            );
            string encodedPayload = Base64Url(
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))
            );
            string dataToSign = $"{encodedHeader}.{encodedPayload}";

            byte[] signature = _keySigner.SignData(
                Encoding.UTF8.GetBytes(dataToSign),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1
            );

            return $"{dataToSign}.{Base64Url(signature)}";
        }
        catch (Exception e)
        {
            throw new ApiException($"Failed to generate JWT assertion: {e.Message}", e);
        }
    }

    private static string Base64Url(byte[] input)
    {
        return Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

/// <summary>
/// Builder for <see cref="WebTokenAuthenticator"/>.
/// </summary>
public class WebTokenAuthenticatorBuilder : OAuthAuthenticatorBuilder<WebTokenAuthenticatorBuilder>
{
    private readonly string _jwtIssuer;
    private readonly string _jwtSubject;
    private readonly string _jwtAudience;
    private readonly RSA _keySigner;
    private TimeSpan _tokenLifetime = TimeSpan.FromHours(1);
    private string? _keyId;

    internal WebTokenAuthenticatorBuilder(
        string host,
        string jwtIssuer,
        string jwtSubject,
        string jwtAudience,
        RSA privateKey
    )
        : base(host)
    {
        _jwtIssuer = jwtIssuer;
        _jwtSubject = jwtSubject;
        _jwtAudience = jwtAudience;
        _keySigner = privateKey;
    }

    /// <summary>
    /// Sets the assertion lifetime.
    /// </summary>
    public WebTokenAuthenticatorBuilder TokenLifetime(TimeSpan tokenLifetime)
    {
        _tokenLifetime = tokenLifetime;
        return this;
    }

    /// <summary>
    /// Sets the key id placed in the JWS header.
    /// </summary>
    public WebTokenAuthenticatorBuilder KeyId(string keyId)
    {
        _keyId = keyId;
        return this;
    }

    /// <summary>
    /// Builds the WebTokenAuthenticator.
    /// </summary>
    public WebTokenAuthenticator Build()
    {
        return new WebTokenAuthenticator(
            OpenId,
            _jwtIssuer,
            _jwtSubject,
            _jwtAudience,
            _keySigner,
            _tokenLifetime,
            _keyId,
            Scope
        );
    }
}
