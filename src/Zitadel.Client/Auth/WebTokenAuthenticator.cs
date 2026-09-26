// Zitadel SDK
// Bespoke authenticator aligned with the generated IHttpAwareAuthenticator interface.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Zitadel.Client.Auth;

/// <summary>
/// JWT-based Authenticator using the JWT Bearer Grant (RFC 7523).
/// <para>Creates a signed JWT assertion and exchanges it for an access token via
/// the shared <see cref="IApiClient"/> inherited from
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
    private readonly string _jwtAlgorithm;
    private readonly string? _keyId;

    /// <summary>
    /// Constructs a WebTokenAuthenticator.
    /// </summary>
    /// <param name="openId">The OpenID discovery helper for the target host.</param>
    /// <param name="jwtIssuer">The issuer claim for the JWT.</param>
    /// <param name="jwtSubject">The subject claim for the JWT.</param>
    /// <param name="jwtAudience">The audience claim for the JWT.</param>
    /// <param name="keySigner">The RSA key used to sign the JWT.</param>
    /// <param name="tokenLifetime">The lifetime of the assertion.</param>
    /// <param name="jwtAlgorithm">The JWT signing algorithm.</param>
    /// <param name="keyId">The optional key id (kid) header.</param>
    /// <param name="scope">The space-delimited scope string for the token request.</param>
    internal WebTokenAuthenticator(
        OpenId openId,
        string jwtIssuer,
        string jwtSubject,
        string jwtAudience,
        RSA keySigner,
        TimeSpan tokenLifetime,
        string jwtAlgorithm,
        string? keyId,
        string scope
    )
        : base(openId, scope)
    {
        _jwtIssuer = jwtIssuer;
        _jwtSubject = jwtSubject;
        _jwtAudience = jwtAudience;
        _keySigner = keySigner;
        _tokenLifetime = tokenLifetime;
        _jwtAlgorithm = jwtAlgorithm;
        _keyId = keyId;
    }

    /// <summary>
    /// Creates a WebTokenAuthenticator from a Zitadel service-account key file.
    /// <para>Expected JSON format:</para>
    /// <code>
    /// {
    ///   "type": "serviceaccount",
    ///   "keyId": "&lt;key-id&gt;",
    ///   "key": "&lt;private-key&gt;",
    ///   "userId": "&lt;user-id&gt;"
    /// }
    /// </code>
    /// </summary>
    /// <param name="host">The base URL for the API endpoints.</param>
    /// <param name="jsonPath">The path to the key file.</param>
    /// <returns>A new WebTokenAuthenticator.</returns>
    /// <exception cref="ArgumentException">If the file cannot be read, is not a JSON
    /// object, lacks the string fields userId, keyId and key, or holds an invalid
    /// key.</exception>
    public static WebTokenAuthenticator FromJson(string host, string jsonPath)
    {
        string content;
        try
        {
            content = File.ReadAllText(jsonPath);
        }
        catch (Exception e)
            when (e
                    is IOException
                        or UnauthorizedAccessException
                        or ArgumentException
                        or NotSupportedException
            )
        {
            throw new ArgumentException($"Unable to read the key file at {jsonPath}", e);
        }

        JsonElement config;
        try
        {
            using JsonDocument document = JsonDocument.Parse(content);
            config = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            config = default;
        }
        if (config.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException($"The key file at {jsonPath} is not a JSON object");
        }

        string? userId = StringField(config, "userId");
        string? keyId = StringField(config, "keyId");
        string? key = StringField(config, "key");
        if (userId == null || keyId == null || key == null)
        {
            throw new ArgumentException(
                $"The key file at {jsonPath} must contain the string fields userId, keyId and key"
            );
        }

        RSA privateKey = RSA.Create();
        try
        {
            privateKey.ImportFromPem(key);
        }
        catch (Exception e) when (e is ArgumentException or CryptographicException)
        {
            privateKey.Dispose();
            throw new ArgumentException("Private key is not a valid RSA private key.", e);
        }
        return CreateBuilder(host, userId, privateKey).KeyId(keyId).Build();
    }

    private static string? StringField(JsonElement config, string name)
    {
        return
            config.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    /// <summary>
    /// Returns a new builder for a WebTokenAuthenticator.
    /// </summary>
    /// <param name="host">The base URL for the API endpoints.</param>
    /// <param name="userId">The user ID, used as both the issuer and the subject.</param>
    /// <param name="privateKey">The RSA private key used to sign the assertion.</param>
    /// <returns>A new builder.</returns>
    /// <exception cref="ArgumentException">If the host is not a valid http or https URL,
    /// the user ID is empty, or the key is not an RSA private key.</exception>
    public static WebTokenAuthenticatorBuilder CreateBuilder(
        string host,
        string userId,
        RSA privateKey
    )
    {
        return new WebTokenAuthenticatorBuilder(host, userId, privateKey);
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
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Dictionary<string, string> header = new() { ["alg"] = _jwtAlgorithm, ["typ"] = "JWT" };
        if (_keyId != null)
        {
            header["kid"] = _keyId;
        }
        Dictionary<string, object> payload = new()
        {
            ["iss"] = _jwtIssuer,
            ["sub"] = _jwtSubject,
            ["aud"] = _jwtAudience,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.Add(_tokenLifetime).ToUnixTimeSeconds(),
        };
        string dataToSign =
            $"{Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(header)))}."
            + $"{Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)))}";
        byte[] signature;
        try
        {
            signature = _keySigner.SignData(
                Encoding.UTF8.GetBytes(dataToSign),
                HashAlgorithm(_jwtAlgorithm),
                RSASignaturePadding.Pkcs1
            );
        }
        catch (CryptographicException e)
        {
            throw new InvalidOperationException("Unable to sign the JWT assertion", e);
        }
        return $"{dataToSign}.{Base64Url(signature)}";
    }

    private static HashAlgorithmName HashAlgorithm(string jwtAlgorithm)
    {
        return jwtAlgorithm switch
        {
            "RS384" => HashAlgorithmName.SHA384,
            "RS512" => HashAlgorithmName.SHA512,
            _ => HashAlgorithmName.SHA256,
        };
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
    private static readonly string[] Algorithms = ["RS256", "RS384", "RS512"];

    private readonly string _userId;
    private readonly RSA _keySigner;
    private TimeSpan _tokenLifetime = TimeSpan.FromHours(1);
    private string _jwtAlgorithm = "RS256";
    private string? _keyId;

    /// <summary>
    /// Initialises the builder.
    /// </summary>
    /// <param name="host">The base URL for the API endpoints.</param>
    /// <param name="userId">The user ID, used as both the issuer and the subject.</param>
    /// <param name="privateKey">The RSA private key used to sign the assertion.</param>
    internal WebTokenAuthenticatorBuilder(string host, string userId, RSA privateKey)
        : base(host)
    {
        _userId = OAuthAuthenticator.RequireText(userId, "User ID");
        _keySigner = RequirePrivateKey(privateKey);
    }

    private static RSA RequirePrivateKey(RSA? privateKey)
    {
        if (privateKey == null)
        {
            throw new ArgumentException("Private key is not a valid RSA private key.");
        }
        try
        {
            _ = privateKey.ExportParameters(true);
        }
        catch (CryptographicException e)
        {
            throw new ArgumentException("Private key is not a valid RSA private key.", e);
        }
        return privateKey;
    }

    /// <summary>
    /// Sets the lifetime of the JWT assertion.
    /// </summary>
    /// <param name="seconds">The lifetime in seconds; must be positive.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">If the lifetime is not positive.</exception>
    public WebTokenAuthenticatorBuilder TokenLifetimeSeconds(long seconds)
    {
        if (seconds <= 0)
        {
            throw new ArgumentException(
                "Token lifetime must be a positive number of seconds.",
                nameof(seconds)
            );
        }
        _tokenLifetime = TimeSpan.FromSeconds(seconds);
        return this;
    }

    /// <summary>
    /// Sets the JWT signing algorithm.
    /// </summary>
    /// <param name="jwtAlgorithm">One of RS256, RS384 or RS512.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">If the algorithm is not supported.</exception>
    public WebTokenAuthenticatorBuilder JwtAlgorithm(string jwtAlgorithm)
    {
        if (!Algorithms.Contains(jwtAlgorithm, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"Unsupported JWT algorithm '{jwtAlgorithm}'; use RS256, RS384 or RS512.",
                nameof(jwtAlgorithm)
            );
        }
        _jwtAlgorithm = jwtAlgorithm;
        return this;
    }

    /// <summary>
    /// Sets the key ID sent as the <c>kid</c> header of the assertion.
    /// </summary>
    /// <param name="keyId">The key ID.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">If the key ID is empty.</exception>
    public WebTokenAuthenticatorBuilder KeyId(string keyId)
    {
        _keyId = OAuthAuthenticator.RequireText(keyId, "Key ID");
        return this;
    }

    /// <summary>
    /// Builds the WebTokenAuthenticator.
    /// </summary>
    /// <returns>A new WebTokenAuthenticator.</returns>
    public WebTokenAuthenticator Build()
    {
        return new WebTokenAuthenticator(
            OpenId,
            _userId,
            _userId,
            OpenId.HostEndpoint,
            _keySigner,
            _tokenLifetime,
            _jwtAlgorithm,
            _keyId,
            Scope
        );
    }
}
