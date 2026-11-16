namespace PenguinPlank.Application.Integrations;

/// <summary>The result of attempting to advance a sync cursor.</summary>
public enum CheckpointAdvanceOutcome
{
    /// <summary>The cursor moved to the new position.</summary>
    Advanced = 0,

    /// <summary>The stored cursor no longer matches the caller's expectation; nothing changed.</summary>
    StaleCursor = 1,
}
