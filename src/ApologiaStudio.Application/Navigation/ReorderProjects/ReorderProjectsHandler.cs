using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Persistence;
using ApologiaStudio.Application.Abstractions.Projects;

namespace ApologiaStudio.Application.Navigation.ReorderProjects;

public sealed class ReorderProjectsHandler(
    IConversationProjectRepository projectRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    public async Task HandleAsync(
        ReorderProjectsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.OrderedProjectIds);

        var projects = await projectRepository.ListByOwnerAsync(
            currentUser.UserId,
            cancellationToken);

        var projectById = projects.ToDictionary(project => project.Id);

        if (command.OrderedProjectIds.Count != projects.Count ||
            command.OrderedProjectIds.Distinct().Count() != projects.Count ||
            command.OrderedProjectIds.Any(id => !projectById.ContainsKey(id)))
        {
            throw new ArgumentException(
                "The project order must contain every owned project exactly once.",
                nameof(command));
        }

        for (var index = 0;
             index < command.OrderedProjectIds.Count;
             index++)
        {
            projectById[command.OrderedProjectIds[index]].Reorder(index);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
