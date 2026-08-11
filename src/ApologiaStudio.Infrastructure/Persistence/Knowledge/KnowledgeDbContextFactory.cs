using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure.Persistence.Knowledge;

public sealed class KnowledgeDbContextFactory
    : IDesignTimeDbContextFactory<KnowledgeDbContext>
{
    public KnowledgeDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The APOLOGIASTUDIO_KNOWLEDGE_DB_CONNECTION environment " +
                "variable must be defined for Knowledge EF design-time commands.");
        }

        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(connectionString, options => options.UseVector())
            .Options;

        return new KnowledgeDbContext(options);
    }
}
