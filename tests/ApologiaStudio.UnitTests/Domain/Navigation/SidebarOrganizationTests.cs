using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Navigation;
using ApologiaStudio.Domain.Projects;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.UnitTests.Domain.Navigation;

public sealed class SidebarOrganizationTests
{
    [Fact]
    public void Project_ShouldNormalizeNameAndPersistOrder()
    {
        var project = ConversationProject.Create(
            UserId.New(),
            "  Papacy  ",
            DateTimeOffset.UtcNow,
            3);

        Assert.Equal("Papacy", project.Name);
        Assert.Equal(3, project.SortOrder);

        project.Reorder(1);

        Assert.Equal(1, project.SortOrder);
    }

    [Fact]
    public void Project_ShouldRejectInvalidNameAndOrder()
    {
        Assert.Throws<ArgumentException>(
            () => ConversationProject.Create(
                UserId.New(),
                " ",
                DateTimeOffset.UtcNow));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ConversationProject.Create(
                UserId.New(),
                "Valid",
                DateTimeOffset.UtcNow,
                -1));
    }

    [Fact]
    public void ConversationPin_ShouldReferenceOnlyConversation()
    {
        var conversation = Conversation.Create(
            UserId.New(),
            "Resurrection",
            DateTimeOffset.UtcNow);

        var pin = SidebarPin.ForConversation(
            conversation,
            DateTimeOffset.UtcNow,
            2);

        Assert.Equal(
            SidebarPinTargetKind.Conversation,
            pin.TargetKind);

        Assert.Equal(conversation.Id, pin.ConversationId!.Value);
        Assert.Null(pin.ProjectId);
        Assert.Equal(2, pin.SortOrder);
    }

    [Fact]
    public void ProjectPin_ShouldReferenceOnlyProject()
    {
        var project = ConversationProject.Create(
            UserId.New(),
            "Islam",
            DateTimeOffset.UtcNow);

        var pin = SidebarPin.ForProject(
            project,
            DateTimeOffset.UtcNow);

        Assert.Equal(
            SidebarPinTargetKind.Project,
            pin.TargetKind);

        Assert.Equal(project.Id, pin.ProjectId!.Value);
        Assert.Null(pin.ConversationId);
    }
}
