namespace PenguinPlank.Application.Catalog.Persistence;

/// <summary>
/// The fully decided set of values a use case hands a <see cref="ICareProfileVersionStore"/> to
/// append a new immutable <c>CareProfileVersion</c>.
/// </summary>
/// <remarks>
/// The use case computes the next version number with the pure
/// <c>CareVersionResolver.NextVersionNumber</c> and resolves the creation instant from the injected
/// time provider <em>before</em> constructing this record (coding-standards §1). The store inserts
/// a new row and never modifies an existing version, honoring the append-only rule (requirements
/// 2.3, 2.5). This is an immutable value with no I/O (coding-standards §1).
/// </remarks>
public sealed record NewCareVersionRecord
{
    /// <summary>The identifier the use case generated for the new version.</summary>
    public required Guid CareProfileVersionId { get; init; }

    /// <summary>The owning care profile.</summary>
    public required Guid CareProfileId { get; init; }

    /// <summary>The next version number computed by the resolver (the first version is 1).</summary>
    public required int VersionNumber { get; init; }

    /// <summary>The care guidance text captured by the new immutable version.</summary>
    public required string Guidance { get; init; }

    /// <summary>The instant the version was created, resolved from the injected time provider.</summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
