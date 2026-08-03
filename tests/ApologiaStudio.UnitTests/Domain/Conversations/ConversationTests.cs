using ApologiaStudio.Domain.Agents;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Projects;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.UnitTests.Domain.Conversations;

public sealed class ConversationTests
{
    [Fact]
    public void Create_ShouldCreateEmptyConversation()
    {
        var now = DateTimeOffset.UtcNow;
        var ownerId = UserId.New();

        var conversation = Conversation.Create(
            ownerId,
            "Historical discussion",
            now);

        Assert.NotEqual(Guid.Empty, conversation.Id.Value);
        Assert.Equal(ownerId, conversation.OwnerId);
        Assert.Equal("Historical discussion", conversation.Title);
        Assert.Equal(now, conversation.CreatedAt);
        Assert.Empty(conversation.Messages);
    }

    [Fact]
    public void AddUserMessage_ShouldAddUserMessage()
    {
        var conversation = CreateConversation();

        var message = conversation.AddUserMessage(
            "When did the doctrine emerge?",
            DateTimeOffset.UtcNow);

        Assert.Single(conversation.Messages);
        Assert.Equal(MessageRole.User, message.Role);
        Assert.Null(message.AgentId);
        Assert.Equal(
            "When did the doctrine emerge?",
            message.Content);
    }

    [Fact]
    public void AddAgentMessage_ShouldRecordAgentAttribution()
    {
        var conversation = CreateConversation();
        var historianId = AgentId.New();

        var message = conversation.AddAgentMessage(
            historianId,
            "The first relevant evidence appears...",
            DateTimeOffset.UtcNow);

        Assert.Single(conversation.Messages);
        Assert.Equal(MessageRole.Agent, message.Role);
        Assert.Equal(historianId, message.AgentId);
    }

    [Fact]
    public void AddUserMessage_ShouldRejectEmptyContent()
    {
        var conversation = CreateConversation();

        var exception = Assert.Throws<ArgumentException>(
            () => conversation.AddUserMessage(
                " ",
                DateTimeOffset.UtcNow));

        Assert.Contains(
            "cannot be empty",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rename_ShouldChangeConversationTitle()
    {
        var conversation = CreateConversation();

        conversation.Rename("Debate preparation");

        Assert.Equal("Debate preparation", conversation.Title);
    }

    [Fact]
    public void MoveToProject_ShouldAssignOwnedProject()
    {
        var ownerId = UserId.New();
        var conversation = Conversation.Create(
            ownerId,
            "Initial conversation",
            DateTimeOffset.UtcNow);

        var project = ConversationProject.Create(
            ownerId,
            "Sola Scriptura",
            DateTimeOffset.UtcNow);

        conversation.MoveToProject(project);

        Assert.Equal(project.Id, conversation.ProjectId!.Value);

        conversation.MoveToChats();

        Assert.Null(conversation.ProjectId);
    }

    [Fact]
    public void MoveToProject_ShouldRejectAnotherUsersProject()
    {
        var conversation = CreateConversation();

        var project = ConversationProject.Create(
            UserId.New(),
            "Private project",
            DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(
            () => conversation.MoveToProject(project));
    }

    [Fact]
    public void Reorder_ShouldRejectNegativeOrder()
    {
        var conversation = CreateConversation();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => conversation.Reorder(-1));
    }

    private static Conversation CreateConversation()
    {
        return Conversation.Create(
            UserId.New(),
            "Initial conversation",
            DateTimeOffset.UtcNow);
    }
}
