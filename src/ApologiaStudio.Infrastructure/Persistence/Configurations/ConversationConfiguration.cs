using ApologiaStudio.Domain.Conversations;
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

        builder.HasIndex(
                conversation => new
                {
                    conversation.OwnerId,
                    conversation.CreatedAt
                })
            .HasDatabaseName(
                "ix_conversations_owner_created_at");

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
