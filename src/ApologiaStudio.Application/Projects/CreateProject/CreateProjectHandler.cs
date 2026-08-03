using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Persistence;
using ApologiaStudio.Application.Abstractions.Projects;
using ApologiaStudio.Domain.Projects;

namespace ApologiaStudio.Application.Projects.CreateProject;

public sealed class CreateProjectHandler(
    IConversationProjectRepository projectRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<ConversationProject> HandleAsync(
        CreateProjectCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var projects = await projectRepository.ListByOwnerAsync(
            currentUser.UserId,
            cancellationToken);

        var normalizedName = command.Name?.Trim() ?? string.Empty;

        if (projects.Any(
                project => string.Equals(
                    project.Name,
                    normalizedName,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"A project named '{normalizedName}' already exists.");
        }

        var project = ConversationProject.Create(
            currentUser.UserId,
            normalizedName,
            timeProvider.GetUtcNow(),
            projects.Count == 0
                ? 0
                : projects.Max(candidate => candidate.SortOrder) + 1);

        projectRepository.Add(project);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return project;
    }
}
