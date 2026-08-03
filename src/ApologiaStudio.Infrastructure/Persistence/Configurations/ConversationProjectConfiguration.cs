using ApologiaStudio.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApologiaStudio.Infrastructure.Persistence.Configurations;

internal sealed class ConversationProjectConfiguration
    : IEntityTypeConfiguration<ConversationProject>
{
    public void Configure(
        EntityTypeBuilder<ConversationProject> builder)
    {
        builder.ToTable(
            "conversation_projects",
            tableBuilder =>
                tableBuilder.HasCheckConstraint(
                    "ck_conversation_projects_sort_order",
                    "sort_order >= 0"));

        builder.HasKey(project => project.Id);

        builder.Property(project => project.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasConversion(
                StronglyTypedIdConverters
                    .ConversationProjectIdConverter)
            .ValueGeneratedNever();

        builder.Property(project => project.OwnerId)
            .HasColumnName("owner_id")
            .HasColumnType("uuid")
            .HasConversion(
                StronglyTypedIdConverters.UserIdConverter)
            .IsRequired();

        builder.Property(project => project.Name)
            .HasColumnName("name")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(project => project.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(project => project.SortOrder)
            .HasColumnName("sort_order")
            .HasDefaultValue(0)
            .IsRequired();

        builder.HasIndex(
                project => new
                {
                    project.OwnerId,
                    project.Name
                })
            .IsUnique()
            .HasDatabaseName(
                "ux_conversation_projects_owner_name");

        builder.HasIndex(
                project => new
                {
                    project.OwnerId,
                    project.SortOrder,
                    project.CreatedAt
                })
            .HasDatabaseName(
                "ix_conversation_projects_owner_sort_order");
    }
}
