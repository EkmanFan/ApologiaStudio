using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApologiaStudio.Infrastructure.Persistence.Configurations;

internal sealed class ConversationConfiguration
    : IEntityTypeConfiguration<Conversation>
{
    public void Configure(
        EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");

        builder.HasKey(conversation => conversation.Id);

        builder.Property(conversation => conversation.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasConversion(
                StronglyTypedIdConverters.ConversationIdConverter)
            .ValueGeneratedNever();

        builder.Property(conversation => conversation.OwnerId)
            .HasColumnName("owner_id")
            .HasColumnType("uuid")
            .HasConversion(
                StronglyTypedIdConverters.UserIdConverter)
            .IsRequired();

        builder.Property(conversation => conversation.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(conversation => conversation.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(conversation => conversation.ProjectId)
            .HasColumnName("project_id")
            .HasColumnType("uuid")
            .HasConversion(
                StronglyTypedIdConverters
                    .NullableConversationProjectIdConverter);

        builder.Property(conversation => conversation.SortOrder)
            .HasColumnName("sort_order")
            .HasDefaultValue(0)
            .IsRequired();

        builder.HasIndex(
                conversation => new
                {
                    conversation.OwnerId,
                    conversation.ProjectId,
                    conversation.SortOrder,
                    conversation.CreatedAt
                })
            .HasDatabaseName(
                "ix_conversations_owner_project_sort_order");

        builder.HasOne<ConversationProject>()
            .WithMany()
            .HasForeignKey(
                conversation => conversation.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(
                conversation => conversation.Messages)
            .WithOne()
            .HasForeignKey("ConversationId")
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.Navigation(
                conversation => conversation.Messages)
            .HasField("_messages")
            .UsePropertyAccessMode(
                PropertyAccessMode.Field);
    }
}
