using ApologiaStudio.Application.Agents.Settings;
using ApologiaStudio.Infrastructure.Persistence.AiRuntime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApologiaStudio.Infrastructure.Persistence.Configurations;

internal sealed class AiAgentSettingsConfiguration
    : IEntityTypeConfiguration<AiAgentSettingsEntity>
{
    public void Configure(EntityTypeBuilder<AiAgentSettingsEntity> builder)
    {
        builder.ToTable(
            "ai_agent_settings",
            tableBuilder =>
                tableBuilder.HasCheckConstraint(
                    "ck_ai_agent_settings_bubble_color",
                    "bubble_color ~ '^#[0-9A-F]{6}$'"));

        builder.HasKey(settings => settings.AgentId);
        builder.Property(settings => settings.AgentId)
            .HasColumnName("agent_id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(settings => settings.Slug)
            .HasColumnName("slug")
            .HasMaxLength(AgentSettingsValidator.MaximumSlugLength);
        builder.HasIndex(settings => settings.Slug)
            .IsUnique();

        builder.Property(settings => settings.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(AgentSettingsValidator.MaximumDisplayNameLength)
            .IsRequired();
        builder.Property(settings => settings.Avatar)
            .HasColumnName("avatar")
            .HasMaxLength(AgentSettingsValidator.MaximumAvatarLength)
            .IsRequired();

        builder.Property(settings => settings.BubbleColor)
            .HasColumnName("bubble_color")
            .HasMaxLength(7)
            .IsFixedLength()
            .IsRequired();
        builder.Property(settings => settings.Model)
            .HasColumnName("model")
            .HasMaxLength(AgentSettingsValidator.MaximumModelLength);

        builder.Property(settings => settings.SystemPrompt)
            .HasColumnName("system_prompt")
            .HasColumnType("text")
            .IsRequired();
        builder.Property(settings => settings.RoutingDescription)
            .HasColumnName("routing_description")
            .HasColumnType("text");

        builder.Property(settings => settings.IsBuiltIn)
            .HasColumnName("is_built_in")
            .HasDefaultValue(false)
            .IsRequired();
        builder.Property(settings => settings.IsEnabled)
            .HasColumnName("is_enabled")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(settings => settings.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
    }
}
