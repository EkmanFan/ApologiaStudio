using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Persistence;
using ApologiaStudio.Application.Abstractions.Projects;

namespace ApologiaStudio.Application.Projects.RenameProject;

public sealed class RenameProjectHandler(
    IConversationProjectRepository projectRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser)
{
    public async Task HandleAsync(
        RenameProjectCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var projects = await projectRepository.ListByOwnerAsync(
            currentUser.UserId,
            cancellationToken);

        var project = projects.SingleOrDefault(
            candidate => candidate.Id == command.ProjectId)
            ?? throw new KeyNotFoundException(
                $"Project '{command.ProjectId}' was not found.");

        var normalizedName = command.Name?.Trim() ?? string.Empty;

        if (projects.Any(
                candidate =>
                    candidate.Id != project.Id &&
                    string.Equals(
                        candidate.Name,
                        normalizedName,
                        StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"A project named '{normalizedName}' already exists.");
        }

        project.Rename(normalizedName);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
