using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Preferences;

namespace ApologiaStudio.Application.Preferences;

public sealed class GetUserPreferencesHandler(
    IUserPreferencesRepository preferencesRepository,
    ICurrentUser currentUser)
{
    public async Task<UserPreferencesView> HandleAsync(
        CancellationToken cancellationToken)
    {
        var preferences = await preferencesRepository.GetAsync(
            currentUser.UserId,
            cancellationToken);

        return preferences is null
            ? UserPreferencesView.Default
            : UserPreferencesView.From(preferences);
    }
}
