using ApologiaStudio.Domain.Projects;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.Application.Abstractions.Projects;

public interface IConversationProjectRepository
{
    Task<IReadOnlyList<ConversationProject>> ListByOwnerAsync(
        UserId ownerId,
        CancellationToken cancellationToken);

    void Add(ConversationProject project);

    void Remove(ConversationProject project);
}
