using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Persistence;
using ApologiaStudio.Application.Abstractions.Preferences;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.Application.Preferences;

public sealed record UpdateUserPreferencesCommand(
    ApplicationLanguage InterfaceLanguage,
    ApplicationLanguage? TheologicalLanguage);

public sealed class UpdateUserPreferencesHandler(
    IUserPreferencesRepository preferencesRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<UserPreferencesView> HandleAsync(
        UpdateUserPreferencesCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.InterfaceLanguage.EnsureSupported(
            nameof(command.InterfaceLanguage));

        if (command.TheologicalLanguage is { } language)
        {
            language.EnsureSupported(
                nameof(command.TheologicalLanguage));
        }

        var preferences = await preferencesRepository.GetAsync(
            currentUser.UserId,
            cancellationToken);

        if (preferences is null)
        {
            preferences = UserPreferences.Create(
                currentUser.UserId,
                command.InterfaceLanguage,
                command.TheologicalLanguage,
                timeProvider.GetUtcNow());

            preferencesRepository.Add(preferences);
        }
        else
        {
            preferences.Update(
                command.InterfaceLanguage,
                command.TheologicalLanguage,
                timeProvider.GetUtcNow());
        }

        await unitOfWork.SaveChangesAsync(
            cancellationToken);

        return UserPreferencesView.From(preferences);
    }
}
