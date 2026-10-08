using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PenguinPlank.Domain.Marketing;

namespace PenguinPlank.Infrastructure.Marketing;

/// <summary>
/// EF Core mapping for the designed-only <see cref="ListingTask"/>.
/// </summary>
/// <remarks>
/// Designed-only schema (task 3.4). The catalog-reference and assignee links are designed-only
/// scalar seams (no FK in Phase A). A due-date index supports task lookups.
/// </remarks>
internal sealed class ListingTaskConfiguration : IEntityTypeConfiguration<ListingTask>
{
    public void Configure(EntityTypeBuilder<ListingTask> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ListingTasks");

        builder.Property(task => task.ReferenceType)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(task => task.Channel)
            .HasMaxLength(64);

        builder.Property(task => task.TaskType)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(task => task.State)
            .HasMaxLength(32);

        builder.Property(task => task.DueDate)
            .HasColumnType("datetimeoffset");

        builder.Property(task => task.CompletedAtUtc)
            .HasColumnType("datetimeoffset");

        builder.HasIndex(task => task.DueDate);
    }
}
