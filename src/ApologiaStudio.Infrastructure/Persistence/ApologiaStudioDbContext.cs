using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Navigation;
using ApologiaStudio.Domain.Projects;
using ApologiaStudio.Domain.Users;
using ApologiaStudio.Infrastructure.Persistence.AiRuntime;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure.Persistence;

public sealed class ApologiaStudioDbContext(
    DbContextOptions<ApologiaStudioDbContext> options)
    : DbContext(options)
{
    public DbSet<Conversation> Conversations =>
        Set<Conversation>();

    public DbSet<ConversationMessage> ConversationMessages =>
        Set<ConversationMessage>();

    public DbSet<ConversationProject> ConversationProjects =>
        Set<ConversationProject>();

    public DbSet<SidebarPin> SidebarPins =>
        Set<SidebarPin>();

    public DbSet<UserPreferences> UserPreferences =>
        Set<UserPreferences>();

    internal DbSet<AiRuntimeSettingsEntity> AiRuntimeSettings =>
        Set<AiRuntimeSettingsEntity>();

    internal DbSet<AiAgentModelAssignmentEntity> AiAgentModelAssignments =>
        Set<AiAgentModelAssignmentEntity>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApologiaStudioDbContext).Assembly);
    }
}
