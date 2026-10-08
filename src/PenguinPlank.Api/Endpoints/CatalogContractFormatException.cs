namespace PenguinPlank.Api.Endpoints;

/// <summary>
/// Signals that a catalog request DTO carried a value the API cannot map to the Application
/// request shape — for example a tracking-mode string that names no known mode.
/// </summary>
/// <remarks>
/// This is a boundary-translation failure, not a domain business failure: the request is malformed
/// before any use case runs. The endpoint catches it and returns a <c>400</c> validation
/// ProblemDetails carrying the stable <c>Validation</c> code (requirement A1 §4.8), keeping the
/// malformed-request response consistent with the rest of the error surface rather than letting it
/// surface as an unexpected <c>500</c>.
/// </remarks>
public sealed class CatalogContractFormatException : System.Exception
{
    /// <summary>Creates the exception with a caller-safe description of the malformed value.</summary>
    /// <param name="message">A caller-safe description naming the field and the expected values.</param>
    public CatalogContractFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and an inner cause.</summary>
    /// <param name="message">A caller-safe description naming the field and the expected values.</param>
    /// <param name="innerException">The underlying cause.</param>
    public CatalogContractFormatException(string message, System.Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception with no message. Provided for completeness; prefer the message overload.</summary>
    public CatalogContractFormatException()
    {
    }
}
