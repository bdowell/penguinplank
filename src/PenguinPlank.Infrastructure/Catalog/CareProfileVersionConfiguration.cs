using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Catalog;

namespace PenguinPlank.Infrastructure.Catalog;

/// <summary>
/// EF Core mapping for the immutable <see cref="CareProfileVersion"/>.
/// </summary>
/// <remarks>
/// <para>
/// A care-profile version is append-only: once inserted it is never modified or deleted
/// (requirements 2.3, 2.5 / design invariant 10). This configuration reinforces that at the
/// persistence layer by setting <see cref="PropertySaveBehavior.Throw"/> on every mutable
/// column, so an accidental update of a loaded version throws instead of silently rewriting
/// history. A unique index on (<see cref="CareProfileVersion.CareProfileId"/>,
/// <see cref="CareProfileVersion.VersionNumber"/>) keeps version numbers distinct within a
/// profile.
/// </para>
/// <para>
/// The type derives from <c>Entity</c>, not <c>VersionedEntity</c>: an immutable record needs
/// no <c>rowversion</c> concurrency token because nothing edits it. Its
/// <c>CreatedAtUtc</c> records the creation instant; the inherited <c>UpdatedAtUtc</c> is not
/// meaningfully used.
/// </para>
/// </remarks>
internal sealed class CareProfileVersionConfiguration : IEntityTypeConfiguration<CareProfileVersion>
{
    public void Configure(EntityTypeBuilder<CareProfileVersion> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("CareProfileVersions");

        builder.Property(version => version.Guidance)
            .IsRequired()
            .HasMaxLength(4000);

        // Append-only: block post-insert updates of the business columns so the version's
        // content and ownership can never be rewritten after creation.
        builder.Property(version => version.CareProfileId)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(version => version.VersionNumber)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(version => version.Guidance)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        builder.Property(version => version.CreatedAtUtc)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

        builder.HasIndex(version => new { version.CareProfileId, version.VersionNumber })
            .IsUnique();

        builder.HasOne<CareProfile>()
            .WithMany()
            .HasForeignKey(version => version.CareProfileId)
            .IsRequired();
    }
}
