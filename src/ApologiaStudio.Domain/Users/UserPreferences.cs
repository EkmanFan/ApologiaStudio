namespace ApologiaStudio.Domain.Users;

public sealed class UserPreferences
{
    public const string DefaultThemeColor = "#2D766E";

    public const ThemeMode DefaultThemeMode = ThemeMode.Light;

    public const string DefaultDarkPageColor = "#242424";

    public const string DefaultDarkSurfaceColor = "#303030";

    /// <summary>
    /// Darkest neutral shade the dark palette accepts. Pure black is
    /// excluded on purpose: it reads as a hole rather than a background.
    /// </summary>
    public const int MinimumDarkShade = 0x10;

    /// <summary>
    /// Lightest neutral shade the dark palette accepts, past which the
    /// light text of the dark theme loses its contrast.
    /// </summary>
    public const int MaximumDarkShade = 0x58;

    public const ApplicationLanguage DefaultInterfaceLanguage =
        ApplicationLanguage.French;

    public const ComposerEnterBehavior DefaultEnterBehavior =
        ComposerEnterBehavior.NewLine;

    public const string DefaultMessageDateFormat =
        MessageTimestampFormats.DayMonthYear;

    public const string DefaultMessageTimeFormat =
        MessageTimestampFormats.TwentyFourHourWithSeconds;

    private UserPreferences()
    {
    }

    private UserPreferences(
        UserId userId,
        ApplicationLanguage interfaceLanguage,
        ApplicationLanguage? theologicalLanguage,
        ComposerEnterBehavior enterBehavior,
        string messageDateFormat,
        string messageTimeFormat,
        ThemeMode themeMode,
        string themeColor,
        string darkPageColor,
        string darkSurfaceColor,
        DateTimeOffset updatedAt)
    {
        UserId = userId;
        SetLanguages(
            interfaceLanguage,
            theologicalLanguage);
        SetEnterBehavior(enterBehavior);
        SetMessageTimestampFormats(
            messageDateFormat,
            messageTimeFormat);
        SetTheme(themeMode, themeColor);
        SetDarkPalette(darkPageColor, darkSurfaceColor);
        UpdatedAt = updatedAt;
    }

    public UserId UserId { get; private set; }

    public ApplicationLanguage InterfaceLanguage { get; private set; }

    public ApplicationLanguage? TheologicalLanguage { get; private set; }

    public ComposerEnterBehavior EnterBehavior { get; private set; } =
        DefaultEnterBehavior;

    public string MessageDateFormat { get; private set; } =
        DefaultMessageDateFormat;

    public string MessageTimeFormat { get; private set; } =
        DefaultMessageTimeFormat;

    public ThemeMode ThemeMode { get; private set; } =
        DefaultThemeMode;

    public string ThemeColor { get; private set; } =
        DefaultThemeColor;

    public string DarkPageColor { get; private set; } =
        DefaultDarkPageColor;

    public string DarkSurfaceColor { get; private set; } =
        DefaultDarkSurfaceColor;

    public ApplicationLanguage EffectiveTheologicalLanguage =>
        TheologicalLanguage ?? InterfaceLanguage;

    public DateTimeOffset UpdatedAt { get; private set; }

    public static UserPreferences Create(
        UserId userId,
        ApplicationLanguage interfaceLanguage,
        ApplicationLanguage? theologicalLanguage,
        DateTimeOffset updatedAt)
    {
        return Create(
            userId,
            interfaceLanguage,
            theologicalLanguage,
            DefaultEnterBehavior,
            DefaultMessageDateFormat,
            DefaultMessageTimeFormat,
            updatedAt);
    }

    public static UserPreferences Create(
        UserId userId,
        ApplicationLanguage interfaceLanguage,
        ApplicationLanguage? theologicalLanguage,
        ComposerEnterBehavior enterBehavior,
        DateTimeOffset updatedAt)
    {
        return Create(
            userId,
            interfaceLanguage,
            theologicalLanguage,
            enterBehavior,
            DefaultMessageDateFormat,
            DefaultMessageTimeFormat,
            updatedAt);
    }

    public static UserPreferences Create(
        UserId userId,
        ApplicationLanguage interfaceLanguage,
        ApplicationLanguage? theologicalLanguage,
        ComposerEnterBehavior enterBehavior,
        string messageDateFormat,
        string messageTimeFormat,
        DateTimeOffset updatedAt)
    {
        return Create(
            userId,
            interfaceLanguage,
            theologicalLanguage,
            enterBehavior,
            messageDateFormat,
            messageTimeFormat,
            DefaultThemeMode,
            DefaultThemeColor,
            updatedAt);
    }

    public static UserPreferences Create(
        UserId userId,
        ApplicationLanguage interfaceLanguage,
        ApplicationLanguage? theologicalLanguage,
        ComposerEnterBehavior enterBehavior,
        string messageDateFormat,
        string messageTimeFormat,
        ThemeMode themeMode,
        string themeColor,
        DateTimeOffset updatedAt)
    {
        return Create(
            userId,
            interfaceLanguage,
            theologicalLanguage,
            enterBehavior,
            messageDateFormat,
            messageTimeFormat,
            themeMode,
            themeColor,
            DefaultDarkPageColor,
            DefaultDarkSurfaceColor,
            updatedAt);
    }

    public static UserPreferences Create(
        UserId userId,
        ApplicationLanguage interfaceLanguage,
        ApplicationLanguage? theologicalLanguage,
        ComposerEnterBehavior enterBehavior,
        string messageDateFormat,
        string messageTimeFormat,
        ThemeMode themeMode,
        string themeColor,
        string darkPageColor,
        string darkSurfaceColor,
        DateTimeOffset updatedAt)
    {
        return new UserPreferences(
            userId,
            interfaceLanguage,
            theologicalLanguage,
            enterBehavior,
            messageDateFormat,
            messageTimeFormat,
            themeMode,
            themeColor,
            darkPageColor,
            darkSurfaceColor,
            updatedAt);
    }

    public void Update(
        ApplicationLanguage interfaceLanguage,
        ApplicationLanguage? theologicalLanguage,
        DateTimeOffset updatedAt)
    {
        SetLanguages(
            interfaceLanguage,
            theologicalLanguage);
        UpdatedAt = updatedAt;
    }

    public void Update(
        ApplicationLanguage interfaceLanguage,
        ApplicationLanguage? theologicalLanguage,
        ComposerEnterBehavior enterBehavior,
        DateTimeOffset updatedAt)
    {
        SetLanguages(
            interfaceLanguage,
            theologicalLanguage);
        SetEnterBehavior(enterBehavior);
        UpdatedAt = updatedAt;
    }

    public void Update(
        ApplicationLanguage interfaceLanguage,
        ApplicationLanguage? theologicalLanguage,
        ComposerEnterBehavior enterBehavior,
        string messageDateFormat,
        string messageTimeFormat,
        DateTimeOffset updatedAt)
    {
        Update(
            interfaceLanguage,
            theologicalLanguage,
            enterBehavior,
            messageDateFormat,
            messageTimeFormat,
            ThemeMode,
            ThemeColor,
            updatedAt);
    }

    public void Update(
        ApplicationLanguage interfaceLanguage,
        ApplicationLanguage? theologicalLanguage,
        ComposerEnterBehavior enterBehavior,
        string messageDateFormat,
        string messageTimeFormat,
        ThemeMode themeMode,
        string themeColor,
        DateTimeOffset updatedAt)
    {
        Update(
            interfaceLanguage,
            theologicalLanguage,
            enterBehavior,
            messageDateFormat,
            messageTimeFormat,
            themeMode,
            themeColor,
            DarkPageColor,
            DarkSurfaceColor,
            updatedAt);
    }

    public void Update(
        ApplicationLanguage interfaceLanguage,
        ApplicationLanguage? theologicalLanguage,
        ComposerEnterBehavior enterBehavior,
        string messageDateFormat,
        string messageTimeFormat,
        ThemeMode themeMode,
        string themeColor,
        string darkPageColor,
        string darkSurfaceColor,
        DateTimeOffset updatedAt)
    {
        SetLanguages(
            interfaceLanguage,
            theologicalLanguage);
        SetEnterBehavior(enterBehavior);
        SetMessageTimestampFormats(
            messageDateFormat,
            messageTimeFormat);
        SetTheme(themeMode, themeColor);
        SetDarkPalette(darkPageColor, darkSurfaceColor);
        UpdatedAt = updatedAt;
    }

    private void SetLanguages(
        ApplicationLanguage interfaceLanguage,
        ApplicationLanguage? theologicalLanguage)
    {
        interfaceLanguage.EnsureSupported(
            nameof(interfaceLanguage));

        if (theologicalLanguage is { } language)
        {
            language.EnsureSupported(
                nameof(theologicalLanguage));
        }

        InterfaceLanguage = interfaceLanguage;
        TheologicalLanguage = theologicalLanguage;
    }

    private void SetEnterBehavior(
        ComposerEnterBehavior enterBehavior)
    {
        if (!Enum.IsDefined(enterBehavior))
        {
            throw new ArgumentOutOfRangeException(
                nameof(enterBehavior),
                enterBehavior,
                "Unsupported composer Enter behavior.");
        }

        EnterBehavior = enterBehavior;
    }

    private void SetMessageTimestampFormats(
        string messageDateFormat,
        string messageTimeFormat)
    {
        MessageTimestampFormats.EnsureSupportedDateFormat(
            messageDateFormat,
            nameof(messageDateFormat));
        MessageTimestampFormats.EnsureSupportedTimeFormat(
            messageTimeFormat,
            nameof(messageTimeFormat));

        MessageDateFormat = messageDateFormat;
        MessageTimeFormat = messageTimeFormat;
    }

    private void SetTheme(
        ThemeMode themeMode,
        string themeColor)
    {
        if (!Enum.IsDefined(themeMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(themeMode),
                themeMode,
                "Unsupported theme mode.");
        }

        ThemeMode = themeMode;
        ThemeColor = NormalizeThemeColor(themeColor);
    }

    public static string NormalizeThemeColor(string themeColor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(themeColor);

        var normalized = themeColor.Trim().ToUpperInvariant();
        if (normalized.Length != 7 ||
            normalized[0] != '#' ||
            normalized[1..].Any(character =>
                !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException(
                "Theme color must use the #RRGGBB hexadecimal format.",
                nameof(themeColor));
        }

        return normalized;
    }

    public static string NormalizeDarkShade(string color)
    {
        var normalized = NormalizeThemeColor(color);
        if (!string.Equals(
                normalized[1..3],
                normalized[3..5],
                StringComparison.Ordinal) ||
            !string.Equals(
                normalized[3..5],
                normalized[5..7],
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Dark palette colors must be neutral grayscale values.",
                nameof(color));
        }

        var shade = Convert.ToInt32(normalized[1..3], 16);
        if (shade < MinimumDarkShade || shade > MaximumDarkShade)
        {
            throw new ArgumentOutOfRangeException(
                nameof(color),
                color,
                "Dark palette colors must range from " +
                $"#{MinimumDarkShade:X2}{MinimumDarkShade:X2}{MinimumDarkShade:X2} to " +
                $"#{MaximumDarkShade:X2}{MaximumDarkShade:X2}{MaximumDarkShade:X2}.");
        }

        return normalized;
    }

    private void SetDarkPalette(
        string darkPageColor,
        string darkSurfaceColor)
    {
        DarkPageColor = NormalizeDarkShade(darkPageColor);
        DarkSurfaceColor = NormalizeDarkShade(darkSurfaceColor);
    }
}
