using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Integrations;

namespace PenguinPlank.Infrastructure.Integrations;

/// <summary>
/// EF Core mapping for <see cref="EntityMapping"/>.
/// </summary>
/// <remarks>
/// The composite <b>unique index</b> on
/// (<see cref="EntityMapping.Platform"/>, <see cref="EntityMapping.Account"/>,
/// <see cref="EntityMapping.ExternalId"/>) enforces that one external resource maps to a single
/// internal record and rejects a duplicate triple (requirement 3.4 / R11; design index strategy).
/// The internal side uses a typed <see cref="EntityMapping.OwnerType"/> discriminator plus
/// <see cref="EntityMapping.InternalId"/> — never an unchecked polymorphic link — and is scoped to
/// its owning connection.
/// </remarks>
internal sealed class EntityMappingConfiguration : IEntityTypeConfiguration<EntityMapping>
{
    public void Configure(EntityTypeBuilder<EntityMapping> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("EntityMappings");

        builder.Property(mapping => mapping.Platform)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(mapping => mapping.Account)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(mapping => mapping.ExternalId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(mapping => mapping.OwnerType)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(mapping => new { mapping.Platform, mapping.Account, mapping.ExternalId })
            .IsUnique();

        builder.HasOne<IntegrationConnection>()
            .WithMany()
            .HasForeignKey(mapping => mapping.ConnectionId)
            .IsRequired();
    }
}
