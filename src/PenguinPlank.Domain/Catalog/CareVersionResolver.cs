using PenguinPlank.Domain.Common;

namespace PenguinPlank.Domain.Catalog;

/// <summary>
/// The pure domain policy for care-profile versioning: it computes the next version number
/// to <b>append</b> when care guidance is edited, and it resolves which
/// <see cref="CareProfileVersion"/> a produced <see cref="ProductPiece"/> must reference based
/// on the instant it was produced.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this type exists.</b> Care guidance is <em>append-only</em> (requirements R10 / 2.3,
/// 2.5 and design invariant 10). Editing a profile's guidance never mutates or deletes an
/// existing <see cref="CareProfileVersion"/>; it creates a brand-new version with the next
/// version number. A produced piece records the version in effect at its production time and
/// <b>keeps resolving that exact version</b> even after later edits add newer versions
/// (requirement R10 / 2.4). This resolver expresses both rules as <em>decisions over explicit
/// inputs</em>: it takes the existing versions (or the current maximum version number) and an
/// instant as ordinary values, performs no I/O, loads no database, and never mutates an existing
/// version — on an append it only computes the <em>number of the version to add</em>, and on a
/// production lookup it only <em>selects</em> an existing version (coding-standards §1, §3). The
/// calling use case owns the subsequent insert of the new immutable row.
/// </para>
/// <para>
/// <b>Append-only next version.</b> <see cref="NextVersionNumber(int)"/> and
/// <see cref="NextVersionNumber(IEnumerable{CareProfileVersion})"/> return
/// <c>max(existing version numbers) + 1</c>, starting at
/// <see cref="FirstVersionNumber"/> (1) when no versions exist yet. The computation is total and
/// side-effect free: it reads the current state and returns the number to append, leaving every
/// existing version untouched. A request to change an existing version is therefore never
/// honoured in place — the only sanctioned outcome of a care edit is a new appended version.
/// </para>
/// <para>
/// <b>Produced-piece version preservation.</b>
/// <see cref="ResolveForProduction(IEnumerable{CareProfileVersion}, System.DateTimeOffset)"/>
/// returns the version <em>in effect</em> at the supplied production instant: the version with
/// the latest <see cref="Entity.CreatedAtUtc"/> that is less than or equal to the instant. Because
/// the result is computed purely from the versions that already existed at that instant, a later
/// edit that appends a newer version does not change the answer for a piece produced earlier — the
/// historical version is preserved (requirement 2.4). When no version was yet in effect at the
/// instant (every version was created after it), the method fails with
/// <see cref="ErrorCode.Validation"/> rather than guessing a version.
/// </para>
/// <para>
/// These are pure static functions tested directly with ordinary values — no application startup
/// required (coding-standards §7). Properties 7 and 8 (task 7.14) exercise them across many
/// inputs; this task implements the behavior only.
/// </para>
/// </remarks>
public static class CareVersionResolver
{
    /// <summary>
    /// The version number assigned to the first version appended to a care profile.
    /// </summary>
    public const int FirstVersionNumber = 1;

    /// <summary>
    /// Computes the next version number to append given the current highest version number.
    /// </summary>
    /// <param name="currentMaxVersionNumber">
    /// The highest <see cref="CareProfileVersion.VersionNumber"/> that currently exists for the
    /// profile, or <c>0</c> (or any value below <see cref="FirstVersionNumber"/>) when no version
    /// exists yet.
    /// </param>
    /// <returns>
    /// <see cref="FirstVersionNumber"/> when the profile has no versions yet; otherwise
    /// <c>currentMaxVersionNumber + 1</c>. This only computes the number to append and never
    /// modifies any existing version.
    /// </returns>
    public static int NextVersionNumber(int currentMaxVersionNumber) =>
        currentMaxVersionNumber < FirstVersionNumber
            ? FirstVersionNumber
            : currentMaxVersionNumber + 1;

    /// <summary>
    /// Computes the next version number to append given the profile's existing versions.
    /// </summary>
    /// <param name="existingVersions">
    /// The versions that already exist for the profile, in any order. A <see langword="null"/> or
    /// empty collection means the profile has no versions yet.
    /// </param>
    /// <returns>
    /// <see cref="FirstVersionNumber"/> when there are no existing versions; otherwise one greater
    /// than the maximum existing <see cref="CareProfileVersion.VersionNumber"/>. The existing
    /// versions are only read, never modified or removed (append-only; requirements 2.3, 2.5).
    /// </returns>
    public static int NextVersionNumber(IEnumerable<CareProfileVersion>? existingVersions)
    {
        if (existingVersions is null)
        {
            return FirstVersionNumber;
        }

        int currentMax = FirstVersionNumber - 1;
        bool any = false;

        foreach (CareProfileVersion version in existingVersions)
        {
            any = true;
            if (version.VersionNumber > currentMax)
            {
                currentMax = version.VersionNumber;
            }
        }

        return any ? currentMax + 1 : FirstVersionNumber;
    }

    /// <summary>
    /// Resolves the <see cref="CareProfileVersion"/> in effect at the given production instant —
    /// the version a <see cref="ProductPiece"/> produced at that instant must reference and keep
    /// resolving to afterwards (requirement 2.4).
    /// </summary>
    /// <param name="versions">
    /// The care-profile versions available to choose from, supplied as an explicit collection in
    /// any order (pure input — the caller loads them; this method performs no I/O). Each version's
    /// <see cref="Entity.CreatedAtUtc"/> marks when it took effect.
    /// </param>
    /// <param name="producedAtUtc">The instant the piece was produced.</param>
    /// <returns>
    /// <see cref="Result.Success{T}(T)"/> carrying the version with the latest
    /// <see cref="Entity.CreatedAtUtc"/> that is at or before <paramref name="producedAtUtc"/>;
    /// otherwise <see cref="Result.Failure{T}(ErrorCode, string)"/> carrying
    /// <see cref="ErrorCode.Validation"/> when the collection is empty or every version was created
    /// after the instant, meaning no version was yet in effect.
    /// </returns>
    /// <remarks>
    /// Ties on <see cref="Entity.CreatedAtUtc"/> are broken by the higher
    /// <see cref="CareProfileVersion.VersionNumber"/>, so when two versions share a creation instant
    /// the later-numbered (newer) one is the one in effect. The selection reads only the supplied
    /// versions, so appending a newer version later cannot change the result for an earlier instant:
    /// the historically referenced version is preserved.
    /// </remarks>
    public static Result<CareProfileVersion> ResolveForProduction(
        IEnumerable<CareProfileVersion>? versions,
        System.DateTimeOffset producedAtUtc)
    {
        CareProfileVersion? inEffect = null;

        if (versions is not null)
        {
            foreach (CareProfileVersion candidate in versions)
            {
                if (candidate.CreatedAtUtc > producedAtUtc)
                {
                    continue;
                }

                if (inEffect is null || IsLaterInEffect(candidate, inEffect))
                {
                    inEffect = candidate;
                }
            }
        }

        if (inEffect is null)
        {
            return Result.Failure<CareProfileVersion>(
                ErrorCode.Validation,
                "No CareProfileVersion was in effect at the requested production time.");
        }

        return Result.Success(inEffect);
    }

    /// <summary>
    /// Determines whether <paramref name="candidate"/> takes precedence over the
    /// <paramref name="current"/> best match as the version in effect: a later
    /// <see cref="Entity.CreatedAtUtc"/>, or an equal instant with a higher
    /// <see cref="CareProfileVersion.VersionNumber"/>.
    /// </summary>
    /// <param name="candidate">The version being considered.</param>
    /// <param name="current">The best in-effect version found so far.</param>
    /// <returns><see langword="true"/> when the candidate should replace the current best match.</returns>
    private static bool IsLaterInEffect(CareProfileVersion candidate, CareProfileVersion current) =>
        candidate.CreatedAtUtc > current.CreatedAtUtc
        || (candidate.CreatedAtUtc == current.CreatedAtUtc
            && candidate.VersionNumber > current.VersionNumber);
}
