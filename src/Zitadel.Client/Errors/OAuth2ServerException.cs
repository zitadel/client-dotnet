// Zitadel SDK
// OAuth2 error raised by the bespoke token-minting authenticators.

namespace Zitadel.Client.Errors;

/// <summary>
/// Exception for an OAuth2 token endpoint that answered with a non-2xx status.
/// <para>Carries the RFC 6749 section 5.2 error fields when the response body
/// holds a well-formed OAuth2 error object, and the raw body in every case.</para>
/// </summary>
public class OAuth2ServerException : ZitadelException
{
    /// <summary>Creates an exception with a default message.</summary>
    public OAuth2ServerException()
        : this(0, null, null, null, string.Empty) { }

    /// <summary>Creates an exception with the given message.</summary>
    /// <param name="message">The error message.</param>
    public OAuth2ServerException(string message)
        : base(message)
    {
        RawBody = string.Empty;
    }

    /// <summary>Creates an exception with the given message and cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public OAuth2ServerException(string message, Exception innerException)
        : base(message, innerException)
    {
        RawBody = string.Empty;
    }

    /// <summary>Creates an exception for a token endpoint response.</summary>
    /// <param name="statusCode">The HTTP status code of the token response.</param>
    /// <param name="code">The RFC 6749 error code, if present.</param>
    /// <param name="description">The human-readable error description, if present.</param>
    /// <param name="uri">A URI describing the error, if present.</param>
    /// <param name="rawBody">The raw token response body.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1054:URI-like parameters should not be strings",
        Justification = "The RFC 6749 error_uri is reported verbatim and may not be a valid URI."
    )]
    public OAuth2ServerException(
        int statusCode,
        string? code,
        string? description,
        string? uri,
        string rawBody
    )
        : base(BuildMessage(statusCode, code, description, rawBody))
    {
        StatusCode = statusCode;
        Code = code;
        Description = description;
        Uri = uri;
        RawBody = rawBody;
    }

    /// <summary>The HTTP status code of the token response.</summary>
    public int StatusCode { get; }

    /// <summary>The RFC 6749 error code, or null when the body held no OAuth2 error object.</summary>
    public string? Code { get; }

    /// <summary>The human-readable error description, if present.</summary>
    public string? Description { get; }

    /// <summary>A URI describing the error, if present, verbatim from the response.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1056:URI-like properties should not be strings",
        Justification = "The RFC 6749 error_uri is reported verbatim and may not be a valid URI."
    )]
    public string? Uri { get; }

    /// <summary>The raw token response body.</summary>
    public string RawBody { get; }

    private static string BuildMessage(
        int statusCode,
        string? code,
        string? description,
        string rawBody
    )
    {
        if (code == null)
        {
            return $"Token request failed with status {statusCode}: {rawBody}";
        }
        return description != null
            ? $"Token request failed with status {statusCode}: {code} -- {description}"
            : $"Token request failed with status {statusCode}: {code}";
    }
}
