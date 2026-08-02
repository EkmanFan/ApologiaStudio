using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.Application.Abstractions.Preferences;

public interface IUserPreferencesRepository
{
    Task<UserPreferences?> GetAsync(
        UserId userId,
        CancellationToken cancellationToken);

    void Add(UserPreferences preferences);
}
