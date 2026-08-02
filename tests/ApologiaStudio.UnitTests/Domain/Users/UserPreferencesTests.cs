using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.UnitTests.Domain.Users;

public sealed class UserPreferencesTests
{
    [Fact]
    public void EffectiveTheologicalLanguage_ShouldFallBackToInterfaceLanguage()
    {
        var preferences = UserPreferences.Create(
            UserId.New(),
            ApplicationLanguage.English,
            theologicalLanguage: null,
            updatedAt: DateTimeOffset.UtcNow);

        Assert.Equal(
            ApplicationLanguage.English,
            preferences.EffectiveTheologicalLanguage);
    }

    [Fact]
    public void EffectiveTheologicalLanguage_ShouldUseExplicitPreference()
    {
        var preferences = UserPreferences.Create(
            UserId.New(),
            ApplicationLanguage.English,
            ApplicationLanguage.French,
            DateTimeOffset.UtcNow);

        Assert.Equal(
            ApplicationLanguage.French,
            preferences.EffectiveTheologicalLanguage);
    }
}
