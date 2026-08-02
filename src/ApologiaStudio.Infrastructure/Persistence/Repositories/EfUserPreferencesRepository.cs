using ApologiaStudio.Application.Abstractions.Preferences;
using ApologiaStudio.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure.Persistence.Repositories;

public sealed class EfUserPreferencesRepository(
    ApologiaStudioDbContext dbContext)
    : IUserPreferencesRepository
{
    public Task<UserPreferences?> GetAsync(
        UserId userId,
        CancellationToken cancellationToken)
    {
        return dbContext.UserPreferences.SingleOrDefaultAsync(
            preferences => preferences.UserId == userId,
            cancellationToken);
    }

    public void Add(UserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        dbContext.UserPreferences.Add(preferences);
    }
}
