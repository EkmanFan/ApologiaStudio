using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;
using ApologiaStudio.Infrastructure.Persistence;
using ApologiaStudio.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.IntegrationTests.Persistence;

[Collection(PostgreSqlDatabaseCollection.Name)]
public sealed class PostgreSqlConversationRepositoryTests
{
    [Fact]
    public async Task Repository_ShouldPersistListRenameAndReloadConversations()
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
                .EnsureCreatedAsync();
        }

        var ownerId = UserId.New();
        var otherOwnerId = UserId.New();
        var agentId = AgentId.New();

        var createdAt = new DateTimeOffset(
            2026,
            8,
            2,
            12,
            0,
            0,
            TimeSpan.Zero);

        var firstConversation = Conversation.Create(
            ownerId,
            "First conversation",
            createdAt);

        firstConversation.AddUserMessage(
            "Quand a eu lieu le concile de Nicée ?",
            createdAt.AddMinutes(1));

        firstConversation.AddAgentMessage(
            agentId,
            "Le premier concile de Nicée a eu lieu en 325.",
            createdAt.AddMinutes(2));

        var secondConversation = Conversation.Create(
            ownerId,
            "Second conversation",
            createdAt.AddHours(1));

        var otherConversation = Conversation.Create(
            otherOwnerId,
            "Another user's conversation",
            createdAt.AddHours(2));

        await using (
            var writeContext =
                new ApologiaStudioDbContext(options))
        {
            var repository =
                new EfConversationRepository(
                    writeContext);

            var unitOfWork =
                new EfUnitOfWork(
                    writeContext);

            repository.Add(firstConversation);
            repository.Add(secondConversation);
            repository.Add(otherConversation);

            await unitOfWork.SaveChangesAsync(
                CancellationToken.None);
        }

        await using (
            var listContext =
                new ApologiaStudioDbContext(options))
        {
            var repository =
                new EfConversationRepository(
                    listContext);

            var conversations =
                await repository.ListByOwnerAsync(
                    ownerId,
                    CancellationToken.None);

            Assert.Collection(
                conversations,
                conversation =>
                    Assert.Equal(
                        secondConversation.Id,
                        conversation.Id),
                conversation =>
                    Assert.Equal(
                        firstConversation.Id,
                        conversation.Id));

            var latest =
                await repository.GetLatestByOwnerAsync(
                    ownerId,
                    CancellationToken.None);

            Assert.NotNull(latest);
            Assert.Equal(
                secondConversation.Id,
                latest.Id);
        }

        await using (
            var renameContext =
                new ApologiaStudioDbContext(options))
        {
            var repository =
                new EfConversationRepository(
                    renameContext);

            var conversation =
                await repository.GetByIdAsync(
                    firstConversation.Id,
                    CancellationToken.None);

            Assert.NotNull(conversation);

            conversation.Rename(
                "Renamed conversation");

            await renameContext.SaveChangesAsync();
        }

        await using (
            var verificationContext =
                new ApologiaStudioDbContext(options))
        {
            var repository =
                new EfConversationRepository(
                    verificationContext);

            var loaded =
                await repository.GetByIdAsync(
                    firstConversation.Id,
                    CancellationToken.None);

            Assert.NotNull(loaded);

            Assert.Equal(
                "Renamed conversation",
                loaded.Title);

            Assert.Equal(
                ownerId,
                loaded.OwnerId);

            Assert.Collection(
                loaded.Messages,
                userMessage =>
                {
                    Assert.Equal(
                        MessageRole.User,
                        userMessage.Role);

                    Assert.Equal(
                        "Quand a eu lieu le concile de Nicée ?",
                        userMessage.Content);
                },
                agentMessage =>
                {
                    Assert.Equal(
                        MessageRole.Agent,
                        agentMessage.Role);

                    Assert.Equal(
                        agentId,
                        agentMessage.AgentId);
                });
        }
    }
}
