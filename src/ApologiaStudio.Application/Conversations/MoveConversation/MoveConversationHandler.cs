using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Persistence;
using ApologiaStudio.Application.Abstractions.Projects;
using ApologiaStudio.Application.Navigation;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Projects;

namespace ApologiaStudio.Application.Conversations.MoveConversation;

public sealed class MoveConversationHandler(
    IConversationRepository conversationRepository,
    IConversationProjectRepository projectRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    public async Task HandleAsync(
        MoveConversationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var ownerId = currentUser.UserId;
        var conversations =
            await conversationRepository.ListByOwnerAsync(
                ownerId,
                cancellationToken);

        var conversation = conversations.SingleOrDefault(
            candidate => candidate.Id == command.ConversationId)
            ?? throw new KeyNotFoundException(
                $"Conversation '{command.ConversationId}' was not found.");

        ConversationProject? targetProject = null;

        if (command.ProjectId is { } projectId)
        {
            targetProject = (await projectRepository.ListByOwnerAsync(
                    ownerId,
                    cancellationToken))
                .SingleOrDefault(project => project.Id == projectId)
                ?? throw new KeyNotFoundException(
                    $"Project '{projectId}' was not found.");
        }

        var sourceProjectId = conversation.ProjectId;
        var destinationProjectId = targetProject?.Id;

        var destination = SidebarOrdering.OrderConversations(
                conversations.Where(
                    candidate =>
                        candidate.Id != conversation.Id &&
                        candidate.ProjectId == destinationProjectId))
            .ToList();

        if (command.Position < 0 ||
            command.Position > destination.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command.Position),
                "The destination position is outside the target container.");
        }

        if (targetProject is null)
        {
            conversation.MoveToChats();
        }
        else
        {
            conversation.MoveToProject(targetProject);
        }

        destination.Insert(command.Position, conversation);
        SidebarOrdering.AssignConversationOrder(destination);

        if (sourceProjectId != destinationProjectId)
        {
            var source = SidebarOrdering.OrderConversations(
                conversations.Where(
                    candidate =>
                        candidate.Id != conversation.Id &&
                        candidate.ProjectId == sourceProjectId));

            SidebarOrdering.AssignConversationOrder(source);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
