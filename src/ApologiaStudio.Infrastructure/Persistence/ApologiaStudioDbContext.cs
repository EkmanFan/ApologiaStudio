using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Navigation;
using ApologiaStudio.Domain.Projects;
using ApologiaStudio.Domain.Users;
using ApologiaStudio.Infrastructure.Persistence.AiRuntime;
using ApologiaStudio.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure.Persistence;

public sealed class ApologiaStudioDbContext(
    DbContextOptions<ApologiaStudioDbContext> options)
    : IdentityDbContext<
        ApologiaIdentityUser,
        IdentityRole<Guid>,
        Guid>(options)
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

    internal DbSet<AiAgentSettingsEntity> AiAgentSettings =>
        Set<AiAgentSettingsEntity>();

    public DbSet<IdentityAdministrationEventEntity>
        IdentityAdministrationEvents =>
            Set<IdentityAdministrationEventEntity>();

    public DbSet<IdentityGroupEntity> IdentityGroups =>
        Set<IdentityGroupEntity>();

    public DbSet<IdentityGroupMembershipEntity> IdentityGroupMemberships =>
        Set<IdentityGroupMembershipEntity>();

    public DbSet<IdentityGroupRoleEntity> IdentityGroupRoles =>
        Set<IdentityGroupRoleEntity>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApologiaStudioDbContext).Assembly);
    }
}
