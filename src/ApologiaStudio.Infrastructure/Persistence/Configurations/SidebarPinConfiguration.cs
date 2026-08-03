using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Navigation;
using ApologiaStudio.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApologiaStudio.Infrastructure.Persistence.Configurations;

internal sealed class SidebarPinConfiguration
    : IEntityTypeConfiguration<SidebarPin>
{
    public void Configure(
        EntityTypeBuilder<SidebarPin> builder)
    {
        builder.ToTable(
            "sidebar_pins",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_sidebar_pins_sort_order",
                    "sort_order >= 0");

                tableBuilder.HasCheckConstraint(
                    "ck_sidebar_pins_target",
                    "(target_kind = 'Conversation' AND " +
                    "conversation_id IS NOT NULL AND project_id IS NULL) " +
                    "OR (target_kind = 'Project' AND " +
                    "project_id IS NOT NULL AND conversation_id IS NULL)");
            });

        builder.HasKey(pin => pin.Id);

        builder.Property(pin => pin.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasConversion(
                StronglyTypedIdConverters.SidebarPinIdConverter)
            .ValueGeneratedNever();

        builder.Property(pin => pin.OwnerId)
            .HasColumnName("owner_id")
            .HasColumnType("uuid")
            .HasConversion(
                StronglyTypedIdConverters.UserIdConverter)
            .IsRequired();

        builder.Property(pin => pin.TargetKind)
            .HasColumnName("target_kind")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(pin => pin.ConversationId)
            .HasColumnName("conversation_id")
            .HasColumnType("uuid")
            .HasConversion(
                StronglyTypedIdConverters
                    .NullableConversationIdConverter);

        builder.Property(pin => pin.ProjectId)
            .HasColumnName("project_id")
            .HasColumnType("uuid")
            .HasConversion(
                StronglyTypedIdConverters
                    .NullableConversationProjectIdConverter);

        builder.Property(pin => pin.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(pin => pin.SortOrder)
            .HasColumnName("sort_order")
            .HasDefaultValue(0)
            .IsRequired();

        builder.HasIndex(
                pin => new
                {
                    pin.OwnerId,
                    pin.SortOrder,
                    pin.CreatedAt
                })
            .HasDatabaseName(
                "ix_sidebar_pins_owner_sort_order");

        builder.HasIndex(
                pin => new
                {
                    pin.OwnerId,
                    pin.ConversationId
                })
            .IsUnique()
            .HasFilter("conversation_id IS NOT NULL")
            .HasDatabaseName(
                "ux_sidebar_pins_owner_conversation");

        builder.HasIndex(
                pin => new
                {
                    pin.OwnerId,
                    pin.ProjectId
                })
            .IsUnique()
            .HasFilter("project_id IS NOT NULL")
            .HasDatabaseName(
                "ux_sidebar_pins_owner_project");

        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(pin => pin.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ConversationProject>()
            .WithMany()
            .HasForeignKey(pin => pin.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
