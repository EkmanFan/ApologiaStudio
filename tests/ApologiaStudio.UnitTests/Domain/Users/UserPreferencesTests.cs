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

    [Fact]
    public void Create_ShouldUseDefaultLightTheme()
    {
        var preferences = UserPreferences.Create(
            UserId.New(),
            ApplicationLanguage.French,
            theologicalLanguage: null,
            updatedAt: DateTimeOffset.UtcNow);

        Assert.Equal(ThemeMode.Light, preferences.ThemeMode);
        Assert.Equal(
            UserPreferences.DefaultThemeColor,
            preferences.ThemeColor);
    }

    [Fact]
    public void Create_ShouldNormalizeThemeColor()
    {
        var preferences = UserPreferences.Create(
            UserId.New(),
            ApplicationLanguage.English,
            theologicalLanguage: null,
            ComposerEnterBehavior.NewLine,
            MessageTimestampFormats.DayMonthYear,
            MessageTimestampFormats.TwentyFourHour,
            ThemeMode.Dark,
            " #a14fc2 ",
            DateTimeOffset.UtcNow);

        Assert.Equal(ThemeMode.Dark, preferences.ThemeMode);
        Assert.Equal("#A14FC2", preferences.ThemeColor);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#12345")]
    [InlineData("#GG0000")]
    public void NormalizeThemeColor_ShouldRejectInvalidValue(string color)
    {
        Assert.Throws<ArgumentException>(
            () => UserPreferences.NormalizeThemeColor(color));
    }

    [Fact]
    public void Create_ShouldUseDefaultDarkPalette()
    {
        var preferences = UserPreferences.Create(
            UserId.New(),
            ApplicationLanguage.French,
            theologicalLanguage: null,
            updatedAt: DateTimeOffset.UtcNow);

        Assert.Equal(
            UserPreferences.DefaultDarkPageColor,
            preferences.DarkPageColor);
        Assert.Equal(
            UserPreferences.DefaultDarkSurfaceColor,
            preferences.DarkSurfaceColor);
        Assert.NotEqual(
            preferences.DarkPageColor,
            preferences.DarkSurfaceColor);
    }

    [Fact]
    public void Create_ShouldNormalizeDarkPalette()
    {
        var preferences = UserPreferences.Create(
            UserId.New(),
            ApplicationLanguage.French,
            theologicalLanguage: null,
            ComposerEnterBehavior.NewLine,
            MessageTimestampFormats.DayMonthYear,
            MessageTimestampFormats.TwentyFourHour,
            ThemeMode.Dark,
            UserPreferences.DefaultThemeColor,
            " #1c1c1c ",
            "#2a2a2a",
            DateTimeOffset.UtcNow);

        Assert.Equal("#1C1C1C", preferences.DarkPageColor);
        Assert.Equal("#2A2A2A", preferences.DarkSurfaceColor);
    }

    [Theory]
    [InlineData("#2A2B2A")]
    [InlineData("#123456")]
    [InlineData("#2D766E")]
    public void NormalizeDarkShade_ShouldRejectNonNeutralValue(string color)
    {
        Assert.Throws<ArgumentException>(
            () => UserPreferences.NormalizeDarkShade(color));
    }

    [Theory]
    [InlineData("#000000")]
    [InlineData("#0F0F0F")]
    [InlineData("#595959")]
    [InlineData("#FFFFFF")]
    public void NormalizeDarkShade_ShouldRejectShadeOutsideRange(string color)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => UserPreferences.NormalizeDarkShade(color));
    }

    [Theory]
    [InlineData("#101010")]
    [InlineData("#242424")]
    [InlineData("#585858")]
    public void NormalizeDarkShade_ShouldAcceptShadeInsideRange(string color)
    {
        Assert.Equal(color, UserPreferences.NormalizeDarkShade(color));
    }

    [Fact]
    public void Update_ShouldKeepDarkPaletteWhenNotSupplied()
    {
        var preferences = UserPreferences.Create(
            UserId.New(),
            ApplicationLanguage.French,
            theologicalLanguage: null,
            ComposerEnterBehavior.NewLine,
            MessageTimestampFormats.DayMonthYear,
            MessageTimestampFormats.TwentyFourHour,
            ThemeMode.Dark,
            UserPreferences.DefaultThemeColor,
            "#1C1C1C",
            "#2A2A2A",
            DateTimeOffset.UtcNow);

        preferences.Update(
            ApplicationLanguage.English,
            theologicalLanguage: null,
            ComposerEnterBehavior.SendMessage,
            MessageTimestampFormats.IsoYearMonthDay,
            MessageTimestampFormats.TwentyFourHour,
            ThemeMode.Dark,
            "#6F42C1",
            DateTimeOffset.UtcNow);

        Assert.Equal("#1C1C1C", preferences.DarkPageColor);
        Assert.Equal("#2A2A2A", preferences.DarkSurfaceColor);
    }
}
