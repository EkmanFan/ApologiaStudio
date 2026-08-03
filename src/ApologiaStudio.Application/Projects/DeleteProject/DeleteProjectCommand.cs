using ApologiaStudio.Domain.Projects;

namespace ApologiaStudio.Application.Projects.DeleteProject;

public sealed record DeleteProjectCommand(
    ConversationProjectId ProjectId);
