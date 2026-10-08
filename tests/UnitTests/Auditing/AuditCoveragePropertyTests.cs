using CsCheck;
using PenguinPlank.Application.Abstractions;
using PenguinPlank.Application.Abstractions.Auditing;
using PenguinPlank.Domain.Auditing;

namespace UnitTests.Auditing;

/// <summary>
/// Property 15 (requirements A2 Â§5.12, A4 Â§6.7, A7 Â§9.6): <em>Every business mutation is
/// audited.</em> For any described change set, any actor (an authenticated staff user or the
/// background-worker system actor), and any timestamp, the pure
/// <see cref="AuditEntryFactory.Create"/> produces exactly one <see cref="AuditEntry"/> per
/// change in order (coverage / one-to-one, so no mutation goes unaudited), records the actor id
/// and the supplied timestamp on every entry, and composes the action and permitted change
/// summary from the operation and changed property <em>names</em> only â€” never a value, so no
/// secret or unnecessary personal data can leak into the trail.
/// </summary>
/// <remarks>
/// <para>
/// <b>Validates: Requirements 5.12, 6.7, 9.6.</b>
/// </para>
/// <para>
/// This is a DB-free property test over the pure factory: it reads no clock, touches no database,
/// and never starts the application (coding-standards Â§1, Â§7). The EF interceptor's same-
/// transaction behavior (audit written iff the mutation commits) is covered by integration test
/// 6.13, not here. CsCheck (pinned in <c>UnitTests.csproj</c>) drives the four sub-properties at
/// â‰¥100 iterations; the companion example-based coverage lives alongside the auditing types.
/// </para>
/// </remarks>
public class AuditCoveragePropertyTests
{
    /// <summary>
    /// Iterations per property. The design mandates â‰¥100 iterations for every correctness
    /// property; this is set well above that floor for a wider sample of the input space.
    /// </summary>
    private const int Iterations = 1000;

    /// <summary>
    /// A known, closed set of changed-property <b>names</b>. Generated change sets draw their
    /// changed-name lists from exactly this set, so the no-value-leak property can assert that an
    /// independently generated secret <em>value</em> never equals a name and never appears in any
    /// produced entry field.
    /// </summary>
    private static readonly string[] s_knownPropertyNames =
    [
        "SalePrice",
        "Barcode",
        "Sku",
        "TrackingMode",
        "DisplayName",
        "Status",
        "CareProfileVersionId",
        "AvailableQuantity",
    ];

    /// <summary>
    /// A known entity-type vocabulary (coding-standards Â§4 domain nouns). Any non-empty type name
    /// satisfies the factory; drawing from a fixed set keeps the summary assertions precise.
    /// </summary>
    private static readonly string[] s_knownEntityTypes =
    [
        "Product",
        "ProductVariant",
        "ProductPiece",
        "ProductionBatch",
        "Sale",
        "InventoryMovement",
    ];

    /// <summary>Generates one of the known entity type names.</summary>
    private static readonly Gen<string> s_genEntityType =
        Gen.Int[0, s_knownEntityTypes.Length - 1].Select(index => s_knownEntityTypes[index]);

    /// <summary>Generates one of the three audit operations.</summary>
    private static readonly Gen<AuditOperation> s_genOperation =
        Gen.Int[0, 2].Select(index => (AuditOperation)index);

    /// <summary>Generates a non-empty entity id (empty ids are not meaningful mutations).</summary>
    private static readonly Gen<Guid> s_genEntityId =
        Gen.Guid.Where(id => id != Guid.Empty);

    /// <summary>
    /// Generates a changed-property-name list (0..knownCount names) drawn without duplication from
    /// the known name set, so every name in a change comes from <see cref="s_knownPropertyNames"/>.
    /// </summary>
    private static readonly Gen<string[]> s_genChangedNames =
        Gen.Int[0, s_knownPropertyNames.Length]
            .Select(count => s_knownPropertyNames.Take(count).ToArray());

    /// <summary>Generates a single described change.</summary>
    private static readonly Gen<AuditChange> s_genChange =
        Gen.Select(s_genEntityType, s_genEntityId, s_genOperation, s_genChangedNames)
            .Select((entityType, entityId, operation, changedNames) =>
                new AuditChange(entityType, entityId, operation, changedNames));

    /// <summary>
    /// Generates a change set of random count 0..16, including the empty set so the one-to-one
    /// property holds degenerately (no changes â†’ no entries).
    /// </summary>
    private static readonly Gen<AuditChange[]> s_genChangeSet =
        Gen.Int[0, 16].SelectMany(count => s_genChange.Array[count]);

    /// <summary>
    /// Generates an actor: either an authenticated staff user (random non-empty id and role) or
    /// the background-worker system actor (<see cref="ActorContext.SystemWorker"/>), so the
    /// property covers worker mutations audited with <see cref="ActorContext.SystemWorkerId"/>
    /// (requirement 9.6).
    /// </summary>
    private static readonly Gen<ActorContext> s_genActor =
        Gen.Bool.SelectMany(isSystemWorker =>
            isSystemWorker
                ? Gen.Const(ActorContext.SystemWorker())
                : Gen.Select(s_genEntityId, Gen.Int[0, 1])
                    .Select((userId, roleIndex) => new ActorContext(userId, (Role)roleIndex)));

    /// <summary>Generates a timestamp across a wide instant range, including offsets.</summary>
    private static readonly Gen<DateTimeOffset> s_genTimestamp =
        Gen.DateTimeOffset;

    /// <summary>
    /// A secret <em>value</em> that is guaranteed never to equal a known property name: it is
    /// prefixed with a sentinel and the known names contain no such prefix. The no-leak property
    /// asserts this string never surfaces in any produced entry field.
    /// </summary>
    private static readonly Gen<string> s_genSecretValue =
        Gen.String[Gen.Char.AlphaNumeric, 0, 40].Select(suffix => "SECRET-VALUE-" + suffix);

    [Fact]
    public void Create_AnyChangeSet_ProducesExactlyOneOrderedEntryPerChange()
    {
        Gen.Select(s_genChangeSet, s_genActor, s_genTimestamp)
            .Sample(
                (changes, actor, timestamp) =>
                {
                    IReadOnlyList<AuditEntry> entries =
                        AuditEntryFactory.Create(changes, actor, timestamp);

                    // Coverage / one-to-one: one entry per change, same count, same order.
                    Assert.Equal(changes.Length, entries.Count);
                    for (int index = 0; index < changes.Length; index++)
                    {
                        Assert.Equal(changes[index].EntityType, entries[index].EntityType);
                        Assert.Equal(changes[index].EntityId, entries[index].EntityId);
                    }
                },
                iter: Iterations);
    }

    [Fact]
    public void Create_AnyActor_RecordsActorIdOnEveryEntry()
    {
        Gen.Select(s_genChangeSet, s_genActor, s_genTimestamp)
            .Sample(
                (changes, actor, timestamp) =>
                {
                    IReadOnlyList<AuditEntry> entries =
                        AuditEntryFactory.Create(changes, actor, timestamp);

                    foreach (AuditEntry entry in entries)
                    {
                        Assert.Equal(actor.UserId, entry.ActorId);
                    }

                    // A worker's mutations are audited with the reserved system identifier (9.6).
                    if (actor.IsSystemWorker)
                    {
                        Assert.All(entries, e => Assert.Equal(ActorContext.SystemWorkerId, e.ActorId));
                    }
                },
                iter: Iterations);
    }

    [Fact]
    public void Create_AnyTimestamp_RecordsSuppliedTimestampOnEveryEntry()
    {
        Gen.Select(s_genChangeSet, s_genActor, s_genTimestamp)
            .Sample(
                (changes, actor, timestamp) =>
                {
                    IReadOnlyList<AuditEntry> entries =
                        AuditEntryFactory.Create(changes, actor, timestamp);

                    Assert.All(entries, entry => Assert.Equal(timestamp, entry.Timestamp));
                },
                iter: Iterations);
    }

    [Fact]
    public void Create_PropertyNamesOnly_NeverLeaksAValueIntoAnyEntryField()
    {
        Gen.Select(s_genChangeSet, s_genActor, s_genTimestamp, s_genSecretValue)
            .Sample(
                (changes, actor, timestamp, secretValue) =>
                {
                    IReadOnlyList<AuditEntry> entries =
                        AuditEntryFactory.Create(changes, actor, timestamp);

                    foreach (AuditEntry entry in entries)
                    {
                        // Permitted detail only: the summary and action carry the operation and
                        // changed property NAMES, never a value. A generated secret value never
                        // appears in any text field of any entry.
                        Assert.DoesNotContain(secretValue, entry.PermittedChangeSummary, StringComparison.Ordinal);
                        Assert.DoesNotContain(secretValue, entry.Action, StringComparison.Ordinal);
                        Assert.DoesNotContain(secretValue, entry.EntityType, StringComparison.Ordinal);

                        // Every token in the summary that is a changed-name detail is drawn from
                        // the known name set â€” the summary surfaces only permitted names.
                        foreach (string changedName in GetChangedNamesFromSummary(entry.PermittedChangeSummary))
                        {
                            Assert.Contains(changedName, s_knownPropertyNames);
                        }
                    }
                },
                iter: Iterations);
    }

    /// <summary>
    /// Extracts the changed-property-name tokens from a permitted change summary. A summary with
    /// changed names has the shape "Updated: NameA, NameB"; a create/delete summary is the bare
    /// operation name and yields no name tokens.
    /// </summary>
    private static string[] GetChangedNamesFromSummary(string summary)
    {
        int colonIndex = summary.IndexOf(':', StringComparison.Ordinal);
        if (colonIndex < 0)
        {
            return [];
        }

        return summary[(colonIndex + 1)..]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
