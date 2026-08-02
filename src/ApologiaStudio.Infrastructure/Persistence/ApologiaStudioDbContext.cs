using ApologiaStudio.Domain.Conversations;
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

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApologiaStudioDbContext).Assembly);
    }
}
