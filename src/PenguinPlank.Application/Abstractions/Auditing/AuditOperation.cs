namespace PenguinPlank.Application.Abstractions.Auditing;

/// <summary>
/// The kind of persistence operation a business mutation performed on an entity. Recorded in the
/// audit trail's action/summary so the trail distinguishes a create from an update or an archive
/// (a soft delete) at a safe granularity.
/// </summary>
public enum AuditOperation
{
    /// <summary>A new entity was inserted.</summary>
    Created = 0,

    /// <summary>An existing entity was updated.</summary>
    Updated = 1,

    /// <summary>An entity row was deleted.</summary>
    Deleted = 2,
}
