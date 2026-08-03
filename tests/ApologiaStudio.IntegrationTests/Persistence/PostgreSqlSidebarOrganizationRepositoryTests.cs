using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Navigation;
using ApologiaStudio.Domain.Projects;
using ApologiaStudio.Domain.Users;
using ApologiaStudio.Infrastructure.Persistence;
using ApologiaStudio.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.IntegrationTests.Persistence;

[Collection(PostgreSqlDatabaseCollection.Name)]
public sealed class PostgreSqlSidebarOrganizationRepositoryTests
{
    [Fact]
    public async Task ConversationRepository_ShouldSeparateDeletedAndActiveChats()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "APOLOGIASTUDIO_TEST_DB_CONNECTION");

        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "APOLOGIASTUDIO_TEST_DB_CONNECTION was not configured.");

        var options =
            new DbContextOptionsBuilder<ApologiaStudioDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        await using (
            var initializationContext =
                new ApologiaStudioDbContext(options))
        {
            await initializationContext.Database.EnsureDeletedAsync();
            await initializationContext.Database.EnsureCreatedAsync();
        }

        var ownerId = UserId.New();
        var createdAt = new DateTimeOffset(
            2026,
            8,
            3,
            12,
            0,
            0,
            TimeSpan.Zero);

        var conversation = Conversation.Create(
            ownerId,
            "Recoverable",
            createdAt);

        conversation.AddUserMessage(
            "This message must survive.",
            createdAt.AddMinutes(1));

        conversation.Delete(createdAt.AddMinutes(2));

        await using (
            var writeContext =
                new ApologiaStudioDbContext(options))
        {
            new EfConversationRepository(writeContext).Add(conversation);
            await writeContext.SaveChangesAsync();
        }

        await using (
            var deletedReadContext =
                new ApologiaStudioDbContext(options))
        {
            var repository =
                new EfConversationRepository(deletedReadContext);

            Assert.Empty(
                await repository.ListByOwnerAsync(
                    ownerId,
                    CancellationToken.None));

            Assert.Null(
                await repository.GetByIdAsync(
                    conversation.Id,
                    CancellationToken.None));

            var deleted = Assert.Single(
                await repository.ListDeletedByOwnerAsync(
                    ownerId,
                    CancellationToken.None));

            Assert.True(deleted.IsDeleted);

            var includingDeleted =
                await repository.GetByIdIncludingDeletedAsync(
                    conversation.Id,
                    CancellationToken.None);

            Assert.NotNull(includingDeleted);
            Assert.Equal(
                "This message must survive.",
                Assert.Single(includingDeleted.Messages).Content);

            includingDeleted.Restore();
            await deletedReadContext.SaveChangesAsync();
        }

        await using (
            var restoredReadContext =
                new ApologiaStudioDbContext(options))
        {
            var restored = Assert.Single(
                await new EfConversationRepository(restoredReadContext)
                    .ListByOwnerAsync(
                        ownerId,
                        CancellationToken.None));

            Assert.False(restored.IsDeleted);
        }
    }

    [Fact]
    public async Task Repositories_ShouldPersistProjectsPinsAndManualOrder()
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "APOLOGIASTUDIO_TEST_DB_CONNECTION");

        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "APOLOGIASTUDIO_TEST_DB_CONNECTION was not configured.");

        var options =
            new DbContextOptionsBuilder<ApologiaStudioDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        await using (
            var initializationContext =
                new ApologiaStudioDbContext(options))
        {
            await initializationContext.Database.EnsureDeletedAsync();
            await initializationContext.Database.EnsureCreatedAsync();
        }

        var ownerId = UserId.New();
        var now = new DateTimeOffset(
            2026,
            8,
            3,
            12,
            0,
            0,
            TimeSpan.Zero);

        var project = ConversationProject.Create(
            ownerId,
            "Church history",
            now,
            1);

        var conversation = Conversation.Create(
            ownerId,
            "Council of Nicaea",
            now.AddMinutes(1));

        conversation.MoveToProject(project);
        conversation.Reorder(2);

        var projectPin = SidebarPin.ForProject(
            project,
            now.AddMinutes(2),
            0);

        var conversationPin = SidebarPin.ForConversation(
            conversation,
            now.AddMinutes(3),
            1);

        await using (
            var writeContext =
                new ApologiaStudioDbContext(options))
        {
            var projectRepository =
                new EfConversationProjectRepository(writeContext);

            var conversationRepository =
                new EfConversationRepository(writeContext);

            var pinRepository =
                new EfSidebarPinRepository(writeContext);

            projectRepository.Add(project);
            conversationRepository.Add(conversation);
            pinRepository.Add(projectPin);
            pinRepository.Add(conversationPin);

            await writeContext.SaveChangesAsync();
        }

        await using (
            var readContext =
                new ApologiaStudioDbContext(options))
        {
            var projects =
                await new EfConversationProjectRepository(readContext)
                    .ListByOwnerAsync(
                        ownerId,
                        CancellationToken.None);

            var conversations =
                await new EfConversationRepository(readContext)
                    .ListByOwnerAsync(
                        ownerId,
                        CancellationToken.None);

            var pins =
                await new EfSidebarPinRepository(readContext)
                    .ListByOwnerAsync(
                        ownerId,
                        CancellationToken.None);

            Assert.Equal(project.Id, Assert.Single(projects).Id);

            var storedConversation = Assert.Single(conversations);
            Assert.Equal(
                project.Id,
                storedConversation.ProjectId!.Value);
            Assert.Equal(2, storedConversation.SortOrder);

            Assert.Collection(
                pins,
                pin =>
                    Assert.Equal(
                        SidebarPinTargetKind.Project,
                        pin.TargetKind),
                pin =>
                    Assert.Equal(
                        SidebarPinTargetKind.Conversation,
                        pin.TargetKind));
        }
    }
}
