using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Auditing;

namespace PenguinPlank.Infrastructure.Auditing;

/// <summary>
/// EF Core mapping for <see cref="IdempotencyRecord"/>.
/// </summary>
/// <remarks>
/// The <b>unique index on <see cref="IdempotencyRecord.Key"/></b> (design index strategy) is the
/// mechanism that serializes concurrent same-key requests at the database and lets the pipeline
/// decide between a replay (identical payload hash) and a conflict (changed hash)
/// (requirements 6.2–6.4 / A4). <see cref="IdempotencyRecord.CallerId"/> is a plain GUID with no
/// foreign key to the Identity tables in Phase A.
/// </remarks>
internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("IdempotencyRecords");

        builder.Property(record => record.Key)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(record => record.Operation)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(record => record.PayloadHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(record => record.ResultReference)
            .HasMaxLength(400);

        builder.HasIndex(record => record.Key)
            .IsUnique();
    }
}
