using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ApologiaStudio.Infrastructure.Persistence;

public sealed class ApologiaStudioDbContextFactory
    : IDesignTimeDbContextFactory<ApologiaStudioDbContext>
{
    public ApologiaStudioDbContext CreateDbContext(
        string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "APOLOGIASTUDIO_DB_CONNECTION");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The APOLOGIASTUDIO_DB_CONNECTION environment " +
                "variable must be defined for EF design-time commands.");
        }

        var options = new DbContextOptionsBuilder<
                ApologiaStudioDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ApologiaStudioDbContext(options);
    }
}
