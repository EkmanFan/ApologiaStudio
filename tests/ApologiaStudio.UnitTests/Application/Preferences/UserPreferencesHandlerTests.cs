using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Persistence;
using ApologiaStudio.Application.Abstractions.Preferences;
using ApologiaStudio.Application.Preferences;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.UnitTests.Application.Preferences;

public sealed class UserPreferencesHandlerTests
{
    [Fact]
    public async Task Get_ShouldReturnFrenchInterfaceDefaultWhenNoPreferencesExist()
    {
        var handler = new GetUserPreferencesHandler(
            new InMemoryPreferencesRepository(),
            new FakeCurrentUser(UserId.New()));

        var result = await handler.HandleAsync(
            CancellationToken.None);

        Assert.Equal(
            ApplicationLanguage.French,
            result.InterfaceLanguage);
        Assert.Null(result.TheologicalLanguage);
        Assert.Equal(
            ApplicationLanguage.French,
            result.EffectiveTheologicalLanguage);
        Assert.Equal(
            ComposerEnterBehavior.NewLine,
            result.EnterBehavior);
    }

    [Fact]
    public async Task Update_ShouldPersistNullableTheologicalLanguage()
    {
        var userId = UserId.New();
        var repository = new InMemoryPreferencesRepository();
        var unitOfWork = new FakeUnitOfWork();

        var handler = new UpdateUserPreferencesHandler(
            repository,
            unitOfWork,
            new FakeCurrentUser(userId),
            new FixedTimeProvider(
                new DateTimeOffset(
                    2026,
                    8,
                    3,
                    15,
                    0,
                    0,
                    TimeSpan.Zero)));

        var result = await handler.HandleAsync(
            new UpdateUserPreferencesCommand(
                ApplicationLanguage.English,
                TheologicalLanguage: null,
                EnterBehavior: ComposerEnterBehavior.SendMessage),
            CancellationToken.None);

        Assert.Equal(
            ApplicationLanguage.English,
            result.EffectiveTheologicalLanguage);
        Assert.NotNull(repository.Preferences);
        Assert.Null(repository.Preferences.TheologicalLanguage);
        Assert.Equal(
            ComposerEnterBehavior.SendMessage,
            repository.Preferences.EnterBehavior);
        Assert.Equal(
            ComposerEnterBehavior.SendMessage,
            result.EnterBehavior);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    private sealed class InMemoryPreferencesRepository
        : IUserPreferencesRepository
    {
        public UserPreferences? Preferences { get; private set; }

        public Task<UserPreferences?> GetAsync(
            UserId userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                Preferences?.UserId == userId
                    ? Preferences
                    : null);
        }

        public void Add(UserPreferences preferences)
        {
            Preferences = preferences;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCurrentUser(UserId userId)
        : ICurrentUser
    {
        public UserId UserId { get; } = userId;
    }

    private sealed class FixedTimeProvider(
        DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
