using ApologiaStudio.Infrastructure.Persistence.AiRuntime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApologiaStudio.Infrastructure.Persistence.Configurations;

internal sealed class AiAgentModelAssignmentConfiguration
    : IEntityTypeConfiguration<AiAgentModelAssignmentEntity>
{
    public void Configure(
        EntityTypeBuilder<AiAgentModelAssignmentEntity> builder)
    {
        builder.ToTable("ai_agent_model_assignments");

        builder.HasKey(
            assignment => new
            {
                assignment.Provider,
                assignment.AgentId
            });

        builder.Property(assignment => assignment.Provider)
            .HasColumnName("provider")
            .HasMaxLength(32)
            .ValueGeneratedNever();

        builder.Property(assignment => assignment.AgentId)
            .HasColumnName("agent_id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(assignment => assignment.Model)
            .HasColumnName("model")
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(assignment => assignment.AgentId)
            .HasDatabaseName(
                "ix_ai_agent_model_assignments_agent_id");
    }
}
