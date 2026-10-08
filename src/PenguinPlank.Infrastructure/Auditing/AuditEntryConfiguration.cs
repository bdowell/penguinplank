using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Auditing;

namespace PenguinPlank.Infrastructure.Auditing;

/// <summary>
/// EF Core mapping for the immutable <see cref="AuditEntry"/>.
/// </summary>
/// <remarks>
/// An audit entry is append-only (requirement 6.7 / A4): once written it is never modified. This
/// configuration sets <see cref="PropertySaveBehavior.Throw"/> on its business columns so an
/// accidental update throws rather than rewriting the trail, and adds the covering lookup index on
/// (<see cref="AuditEntry.EntityType"/>, <see cref="AuditEntry.EntityId"/>,
/// <see cref="AuditEntry.Timestamp"/>) from the design index strategy for "audit by entity".
/// <see cref="AuditEntry.ActorId"/> is a plain GUID with no foreign key to the Identity tables in
/// Phase A.
/// <para>
/// <b>GUID-key fragmentation strategy (design index strategy).</b> The audit trail is a hot,
/// append-only, insert-heavy table. A clustered index on the random GUID primary key would cause
/// avoidable page splits and fragmentation on every insert, so the primary key is made
/// <em>non-clustered</em> and the clustered key is placed on the insert-ordered
/// <c>CreatedAtUtc</c> column. New rows append to the end of the clustered index and
/// read patterns are time-ordered, which matches how the trail is queried. Applied only to the
/// genuinely hot Phase A tables (audit, inbox, outbox); designed-only tables keep EF's default.
/// The clustered key uses the inherited <c>CreatedAtUtc</c> insert-ordered column.
/// </para>
/// </remarks>
internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AuditEntries");

        builder.Property(entry => entry.Action)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(entry => entry.EntityType)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(entry => entry.Timestamp)
            .HasColumnType("datetimeoffset");

        builder.Property(entry => entry.PermittedChangeSummary)
            .IsRequired()
            .HasMaxLength(2000);

        // Append-only: block post-insert updates of the recorded facts.
        builder.Property(entry => entry.ActorId)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(entry => entry.Action)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(entry => entry.EntityType)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(entry => entry.EntityId)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(entry => entry.Timestamp)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(entry => entry.PermittedChangeSummary)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

        builder.HasIndex(entry => new { entry.EntityType, entry.EntityId, entry.Timestamp });

        // Non-clustered GUID PK + clustered key on the insert-ordered CreatedAtUtc column to
        // avoid random-GUID page-split fragmentation on this hot append-only table.
        builder.HasKey(entry => entry.Id).IsClustered(false);
        builder.HasIndex(entry => entry.CreatedAtUtc).IsClustered();
    }
}
