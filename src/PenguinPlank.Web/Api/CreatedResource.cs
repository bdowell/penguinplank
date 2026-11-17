namespace PenguinPlank.Web.Api;

/// <summary>The body returned by a create endpoint: the identifier of the new record.</summary>
public sealed record CreatedResource
{
    /// <summary>Gets the identifier of the created record.</summary>
    public required Guid Id { get; init; }
}
