using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;
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

    public DbSet<UserPreferences> UserPreferences =>
        Set<UserPreferences>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApologiaStudioDbContext).Assembly);
    }
}
