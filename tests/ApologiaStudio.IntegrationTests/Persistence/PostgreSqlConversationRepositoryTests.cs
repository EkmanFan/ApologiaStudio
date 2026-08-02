using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;
using ApologiaStudio.Infrastructure.Persistence;
using ApologiaStudio.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.IntegrationTests.Persistence;

public sealed class PostgreSqlConversationRepositoryTests
{
    [Fact]
    public async Task Repository_ShouldPersistAndReloadConversation()
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
        var agentId = AgentId.New();

        var createdAt = new DateTimeOffset(
            2026,
            8,
            2,
            12,
            0,
            0,
            TimeSpan.Zero);

        var conversation = Conversation.Create(
            ownerId,
            "Persistent conversation",
            createdAt);

        conversation.AddUserMessage(
            "Quand a eu lieu le concile de Nicée ?",
            createdAt.AddMinutes(1));

        conversation.AddAgentMessage(
            agentId,
            "Le premier concile de Nicée a eu lieu en 325.",
            createdAt.AddMinutes(2));

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

            repository.Add(conversation);

            await unitOfWork.SaveChangesAsync(
                CancellationToken.None);
        }

        await using (
            var readContext =
                new ApologiaStudioDbContext(options))
        {
            var repository =
                new EfConversationRepository(
                    readContext);

            var loaded =
                await repository.GetByIdAsync(
                    conversation.Id,
                    CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(ownerId, loaded.OwnerId);
            Assert.Equal(
                "Persistent conversation",
                loaded.Title);

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

                    Assert.Equal(
                        "Le premier concile de Nicée a eu lieu en 325.",
                        agentMessage.Content);
                });

            var latest =
                await repository.GetLatestByOwnerAsync(
                    ownerId,
                    CancellationToken.None);

            Assert.NotNull(latest);
            Assert.Equal(
                conversation.Id,
                latest.Id);
        }
    }
}
