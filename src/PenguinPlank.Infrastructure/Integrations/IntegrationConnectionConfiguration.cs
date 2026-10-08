using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Integrations;

namespace PenguinPlank.Infrastructure.Integrations;

/// <summary>
/// EF Core mapping for <see cref="IntegrationConnection"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IntegrationConnection.Enabled"/> is mapped with an EF
/// <c>HasDefaultValue(false)</c> so a connection row inserted outside a use case starts disabled —
/// connectors are disabled in Phase A (requirement 3.17 / R11). <see cref="SyncDirection"/> and
/// <see cref="ConnectionHealth"/> are stored as readable strings with their safe defaults.
/// </para>
/// <para>
/// Only a <see cref="IntegrationConnection.CredentialReference"/> is mapped — never a secret value
/// column. The external credential secret lives outside the database (encrypted or in the
/// deployment secret store) and is never persisted here, in logs, or in responses
/// (requirement 3.2 / A4).
/// </para>
/// </remarks>
internal sealed class IntegrationConnectionConfiguration : IEntityTypeConfiguration<IntegrationConnection>
{
    public void Configure(EntityTypeBuilder<IntegrationConnection> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("IntegrationConnections");

        builder.Property(connection => connection.Platform)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(connection => connection.Account)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(connection => connection.Scopes)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(connection => connection.ApiVersion)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(connection => connection.EnabledCapabilities)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(connection => connection.SyncDirection)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(SyncDirection.None);

        builder.Property(connection => connection.Health)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(ConnectionHealth.Unknown);

        // Connectors are disabled in Phase A: a connection must default to disabled even if a row
        // is inserted outside a use case (requirement 3.17).
        builder.Property(connection => connection.Enabled)
            .HasDefaultValue(false);

        // Only a reference to the secret is ever stored — never the secret value itself (3.2).
        builder.Property(connection => connection.CredentialReference)
            .HasMaxLength(400);

        builder.HasIndex(connection => new { connection.Platform, connection.Account });
    }
}
