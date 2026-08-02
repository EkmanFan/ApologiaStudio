using ApologiaStudio.Domain.Conversations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApologiaStudio.Infrastructure.Persistence.Configurations;

internal sealed class ConversationMessageConfiguration
    : IEntityTypeConfiguration<ConversationMessage>
{
    public void Configure(
        EntityTypeBuilder<ConversationMessage> builder)
    {
        builder.ToTable("conversation_messages");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasConversion(
                StronglyTypedIdConverters.MessageIdConverter)
            .ValueGeneratedNever();

        builder.Property<ConversationId>("ConversationId")
            .HasColumnName("conversation_id")
            .HasColumnType("uuid")
            .HasConversion(
                StronglyTypedIdConverters.ConversationIdConverter)
            .IsRequired();

        builder.Property(message => message.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(message => message.Content)
            .HasColumnName("content")
            .HasMaxLength(50000)
            .IsRequired();

        builder.Property(message => message.AgentId)
            .HasColumnName("agent_id")
            .HasColumnType("uuid")
            .HasConversion(
                StronglyTypedIdConverters.NullableAgentIdConverter);

        builder.Property(message => message.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(
                "ConversationId",
                nameof(ConversationMessage.CreatedAt))
            .HasDatabaseName(
                "ix_messages_conversation_created_at");
    }
}
