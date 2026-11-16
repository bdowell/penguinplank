namespace PenguinPlank.Application.Integrations;

/// <summary>
/// Pure rule deciding whether a sync cursor may advance (requirement 3.9). A cursor advances only
/// when the stored value is the one the caller read, so two pollers cannot silently overwrite each
/// other's progress.
/// </summary>
public static class CursorAdvancePolicy
{
    /// <summary>Decides the outcome of advancing a cursor.</summary>
    /// <param name="storedCursor">The cursor currently stored, or <see langword="null"/> when none.</param>
    /// <param name="expectedCursor">The cursor the caller last read, or <see langword="null"/> when none.</param>
    /// <returns><see cref="CheckpointAdvanceOutcome.Advanced"/> when the values match; otherwise <see cref="CheckpointAdvanceOutcome.StaleCursor"/>.</returns>
    public static CheckpointAdvanceOutcome Decide(string? storedCursor, string? expectedCursor)
    {
        return string.Equals(storedCursor, expectedCursor, StringComparison.Ordinal)
            ? CheckpointAdvanceOutcome.Advanced
            : CheckpointAdvanceOutcome.StaleCursor;
    }
}
