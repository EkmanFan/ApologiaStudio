using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Navigation;
using ApologiaStudio.Application.Abstractions.Persistence;
using ApologiaStudio.Application.Abstractions.Projects;
using ApologiaStudio.Application.Navigation;

namespace ApologiaStudio.Application.Projects.DeleteProject;

public sealed class DeleteProjectHandler(
    IConversationProjectRepository projectRepository,
    IConversationRepository conversationRepository,
    ISidebarPinRepository pinRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    public async Task HandleAsync(
        DeleteProjectCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var ownerId = currentUser.UserId;
        var projects = await projectRepository.ListByOwnerAsync(
            ownerId,
            cancellationToken);

        var project = projects.SingleOrDefault(
            candidate => candidate.Id == command.ProjectId)
            ?? throw new KeyNotFoundException(
                $"Project '{command.ProjectId}' was not found.");

        var activeConversations =
            await conversationRepository.ListByOwnerAsync(
                ownerId,
                cancellationToken);

        var deletedConversations =
            await conversationRepository.ListDeletedByOwnerAsync(
                ownerId,
                cancellationToken);

        foreach (var conversation in activeConversations
                     .Concat(deletedConversations)
                     .Where(
                         conversation =>
                             conversation.ProjectId == project.Id))
        {
            conversation.MoveToChats();
        }

        var orderedChats = SidebarOrdering.OrderConversations(
            activeConversations.Where(
                conversation => conversation.ProjectId is null));

        SidebarOrdering.AssignConversationOrder(orderedChats);

        var pins = await pinRepository.ListByOwnerAsync(
            ownerId,
            cancellationToken);

        foreach (var pin in pins.Where(
                     pin => pin.ProjectId == project.Id).ToArray())
        {
            pinRepository.Remove(pin);
        }

        SidebarOrdering.AssignPinOrder(
            SidebarOrdering.OrderPins(
                pins.Where(pin => pin.ProjectId != project.Id)));

        projectRepository.Remove(project);

        SidebarOrdering.AssignProjectOrder(
            SidebarOrdering.OrderProjects(
                projects.Where(candidate => candidate.Id != project.Id)));

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
