// Zitadel SDK
// OAuth2 error raised by the bespoke token-minting authenticators.

namespace Zitadel.Client.Errors;

/// <summary>
/// Exception for an OAuth2 token endpoint that answered 2xx with a body the SDK
/// cannot use: not a JSON object, or without a non-empty <c>access_token</c>.
/// </summary>
public class OAuth2TokenException : ZitadelException
{
    /// <summary>Creates an exception with a default message.</summary>
    public OAuth2TokenException() { }

    /// <summary>Creates an exception with the given message.</summary>
    /// <param name="message">The error message.</param>
    public OAuth2TokenException(string message)
        : base(message) { }

    /// <summary>Creates an exception with the given message and cause.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying exception.</param>
    public OAuth2TokenException(string message, Exception innerException)
        : base(message, innerException) { }
}
