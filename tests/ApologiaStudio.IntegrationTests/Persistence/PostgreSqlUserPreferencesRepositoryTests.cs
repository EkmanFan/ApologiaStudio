using ApologiaStudio.Domain.Users;
using ApologiaStudio.Infrastructure.Persistence;
using ApologiaStudio.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.IntegrationTests.Persistence;

[Collection(PostgreSqlDatabaseCollection.Name)]
public sealed class PostgreSqlUserPreferencesRepositoryTests
{
    [Fact]
    public async Task Repository_ShouldPersistNullableTheologicalLanguage()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "APOLOGIASTUDIO_TEST_DB_CONNECTION");

        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "APOLOGIASTUDIO_TEST_DB_CONNECTION was not configured.");

        var options =
            new DbContextOptionsBuilder<
                    ApologiaStudioDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        await using (
            var initializationContext =
                new ApologiaStudioDbContext(options))
        {
            await initializationContext.Database
                .EnsureDeletedAsync();

            await initializationContext.Database
                .MigrateAsync();
        }

        var userId = UserId.New();

        await using (
            var writeContext =
                new ApologiaStudioDbContext(options))
        {
            var repository =
                new EfUserPreferencesRepository(
                    writeContext);

            repository.Add(
                UserPreferences.Create(
                    userId,
                    ApplicationLanguage.English,
                    theologicalLanguage: null,
                    enterBehavior: ComposerEnterBehavior.SendMessage,
                    messageDateFormat: MessageTimestampFormats.IsoYearMonthDay,
                    messageTimeFormat: MessageTimestampFormats.TwelveHourWithSeconds,
                    updatedAt: DateTimeOffset.UtcNow));

            await writeContext.SaveChangesAsync();
        }

        await using (
            var readContext =
                new ApologiaStudioDbContext(options))
        {
            var repository =
                new EfUserPreferencesRepository(
                    readContext);

            var preferences = await repository.GetAsync(
                userId,
                CancellationToken.None);

            Assert.NotNull(preferences);
            Assert.Equal(
                ApplicationLanguage.English,
                preferences.InterfaceLanguage);
            Assert.Null(preferences.TheologicalLanguage);
            Assert.Equal(
                ApplicationLanguage.English,
                preferences.EffectiveTheologicalLanguage);
            Assert.Equal(
                ComposerEnterBehavior.SendMessage,
                preferences.EnterBehavior);
            Assert.Equal(
                MessageTimestampFormats.IsoYearMonthDay,
                preferences.MessageDateFormat);
            Assert.Equal(
                MessageTimestampFormats.TwelveHourWithSeconds,
                preferences.MessageTimeFormat);
        }
    }
}
