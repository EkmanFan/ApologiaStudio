using ApologiaStudio.Application.AiRuntime.Settings;
using ApologiaStudio.Infrastructure.Persistence.AiRuntime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApologiaStudio.Infrastructure.Persistence.Configurations;

internal sealed class AiRuntimeSettingsConfiguration
    : IEntityTypeConfiguration<AiRuntimeSettingsEntity>
{
    public void Configure(
        EntityTypeBuilder<AiRuntimeSettingsEntity> builder)
    {
        builder.ToTable(
            "ai_runtime_settings",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_ai_runtime_settings_provider",
                    "provider = 'Ollama'");

                tableBuilder.HasCheckConstraint(
                    "ck_ai_runtime_settings_routing_timeout",
                    "routing_timeout_seconds BETWEEN 1 AND 300");

                tableBuilder.HasCheckConstraint(
                    "ck_ai_runtime_settings_generation_timeout",
                    "generation_timeout_seconds BETWEEN 1 AND 600");

                tableBuilder.HasCheckConstraint(
                    "ck_ai_runtime_settings_history_messages",
                    "maximum_history_messages BETWEEN 1 AND 100");

                tableBuilder.HasCheckConstraint(
                    "ck_ai_runtime_settings_history_characters",
                    "maximum_history_characters BETWEEN 1000 AND 100000");

                tableBuilder.HasCheckConstraint(
                    "ck_ai_runtime_settings_output_tokens",
                    "maximum_output_tokens BETWEEN 64 AND 8192");
            });

        builder.HasKey(settings => settings.Provider);

        builder.Property(settings => settings.Provider)
            .HasColumnName("provider")
            .HasMaxLength(32)
            .ValueGeneratedNever();

        builder.Property(settings => settings.BaseAddress)
            .HasColumnName("base_address")
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(settings => settings.RoutingModel)
            .HasColumnName("routing_model")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(settings => settings.DefaultAgentModel)
            .HasColumnName("default_agent_model")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(settings => settings.RoutingTimeoutSeconds)
            .HasColumnName("routing_timeout_seconds")
            .IsRequired();

        builder.Property(settings => settings.GenerationTimeoutSeconds)
            .HasColumnName("generation_timeout_seconds")
            .IsRequired();

        builder.Property(settings => settings.KeepAlive)
            .HasColumnName("keep_alive")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(settings => settings.MaximumHistoryMessages)
            .HasColumnName("maximum_history_messages")
            .IsRequired();

        builder.Property(settings => settings.MaximumHistoryCharacters)
            .HasColumnName("maximum_history_characters")
            .IsRequired();

        builder.Property(settings => settings.MaximumOutputTokens)
            .HasColumnName("maximum_output_tokens")
            .IsRequired();

        builder.Property(settings => settings.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasMany(settings => settings.AgentModels)
            .WithOne()
            .HasForeignKey(assignment => assignment.Provider)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
