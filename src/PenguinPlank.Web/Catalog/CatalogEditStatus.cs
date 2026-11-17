namespace PenguinPlank.Web.Catalog;

/// <summary>The lifecycle of a single catalog edit as the user sees it.</summary>
public enum CatalogEditStatus
{
    /// <summary>The form holds the loaded record, possibly with local changes not yet submitted.</summary>
    Editing,

    /// <summary>A save is in flight; further submits are ignored.</summary>
    Saving,

    /// <summary>The server confirmed the edit.</summary>
    Saved,

    /// <summary>
    /// The record changed since it was loaded (stale version). The user must refresh before
    /// editing again; nothing was overwritten (requirement A5 7.6).
    /// </summary>
    Conflict,

    /// <summary>
    /// The server could not be reached, so the edit is not saved and is not queued. The user may
    /// resubmit it explicitly (requirement A5 7.7).
    /// </summary>
    Unsaved,

    /// <summary>The server answered but did not accept the edit (validation, permission, server error).</summary>
    Rejected,
}
