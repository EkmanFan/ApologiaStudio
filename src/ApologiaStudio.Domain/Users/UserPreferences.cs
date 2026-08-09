namespace ApologiaStudio.Domain.Users;

public sealed class UserPreferences
{
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
        return new UserPreferences(
            userId,
            interfaceLanguage,
            theologicalLanguage,
            enterBehavior,
            messageDateFormat,
            messageTimeFormat,
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
        SetLanguages(
            interfaceLanguage,
            theologicalLanguage);
        SetEnterBehavior(enterBehavior);
        SetMessageTimestampFormats(
            messageDateFormat,
            messageTimeFormat);
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
}
