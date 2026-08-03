using ApologiaStudio.Domain.Projects;

namespace ApologiaStudio.Application.Navigation.ReorderProjects;

public sealed record ReorderProjectsCommand(
    IReadOnlyList<ConversationProjectId> OrderedProjectIds);
