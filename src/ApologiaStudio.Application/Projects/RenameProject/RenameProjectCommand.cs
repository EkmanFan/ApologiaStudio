using ApologiaStudio.Domain.Projects;

namespace ApologiaStudio.Application.Projects.RenameProject;

public sealed record RenameProjectCommand(
    ConversationProjectId ProjectId,
    string Name);
