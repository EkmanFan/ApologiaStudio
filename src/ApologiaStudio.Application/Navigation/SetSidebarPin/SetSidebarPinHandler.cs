using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Navigation;
using ApologiaStudio.Application.Abstractions.Persistence;
using ApologiaStudio.Application.Abstractions.Projects;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Navigation;
using ApologiaStudio.Domain.Projects;

namespace ApologiaStudio.Application.Navigation.SetSidebarPin;

public sealed class SetSidebarPinHandler(
    IConversationRepository conversationRepository,
    IConversationProjectRepository projectRepository,
    ISidebarPinRepository pinRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task HandleAsync(
        SetSidebarPinCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!Enum.IsDefined(
                typeof(SidebarPinTargetKind),
                command.TargetKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(command.TargetKind),
                "Unsupported sidebar pin target kind.");
        }

        var ownerId = currentUser.UserId;
        var pins = await pinRepository.ListByOwnerAsync(
            ownerId,
            cancellationToken);

        var existingPin = pins.SingleOrDefault(
            pin => Matches(pin, command));

        if (!command.IsPinned)
        {
            if (existingPin is null)
            {
                return;
            }

            pinRepository.Remove(existingPin);

            SidebarOrdering.AssignPinOrder(
                SidebarOrdering.OrderPins(
                    pins.Where(pin => pin.Id != existingPin.Id)));

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        if (existingPin is not null)
        {
            return;
        }

        var pin = command.TargetKind switch
        {
            SidebarPinTargetKind.Conversation =>
                SidebarPin.ForConversation(
                    await GetOwnedConversationAsync(
                        new ConversationId(command.TargetId),
                        cancellationToken),
                    timeProvider.GetUtcNow(),
                    GetNextSortOrder(pins)),

            SidebarPinTargetKind.Project =>
                SidebarPin.ForProject(
                    await GetOwnedProjectAsync(
                        new ConversationProjectId(command.TargetId),
                        cancellationToken),
                    timeProvider.GetUtcNow(),
                    GetNextSortOrder(pins)),

            _ => throw new ArgumentOutOfRangeException(
                nameof(command),
                "Unsupported sidebar pin target kind.")
        };

        pinRepository.Add(pin);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static int GetNextSortOrder(
        IReadOnlyList<SidebarPin> pins)
    {
        return pins.Count == 0
            ? 0
            : pins.Max(pin => pin.SortOrder) + 1;
    }

    private async Task<Conversation> GetOwnedConversationAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken)
    {
        var conversation = await conversationRepository.GetByIdAsync(
            conversationId,
            cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Conversation '{conversationId}' was not found.");

        if (conversation.OwnerId != currentUser.UserId)
        {
            throw new UnauthorizedAccessException(
                "The current user cannot pin this conversation.");
        }

        return conversation;
    }

    private async Task<ConversationProject> GetOwnedProjectAsync(
        ConversationProjectId projectId,
        CancellationToken cancellationToken)
    {
        var project = (await projectRepository.ListByOwnerAsync(
                currentUser.UserId,
                cancellationToken))
            .SingleOrDefault(candidate => candidate.Id == projectId)
            ?? throw new KeyNotFoundException(
                $"Project '{projectId}' was not found.");

        return project;
    }

    private static bool Matches(
        SidebarPin pin,
        SetSidebarPinCommand command)
    {
        return command.TargetKind switch
        {
            SidebarPinTargetKind.Conversation =>
                pin.ConversationId?.Value == command.TargetId,

            SidebarPinTargetKind.Project =>
                pin.ProjectId?.Value == command.TargetId,

            _ => false
        };
    }
}
