namespace ApologiaStudio.Domain.Users;

public sealed class UserPreferences
{
    public const ApplicationLanguage DefaultInterfaceLanguage =
        ApplicationLanguage.French;

    public const ComposerEnterBehavior DefaultEnterBehavior =
        ComposerEnterBehavior.NewLine;

    private UserPreferences()
    {
    }

    private UserPreferences(
        UserId userId,
        ApplicationLanguage interfaceLanguage,
        ApplicationLanguage? theologicalLanguage,
        ComposerEnterBehavior enterBehavior,
        DateTimeOffset updatedAt)
    {
        UserId = userId;
        SetLanguages(
            interfaceLanguage,
            theologicalLanguage);
        SetEnterBehavior(enterBehavior);
        UpdatedAt = updatedAt;
    }

    public UserId UserId { get; private set; }

    public ApplicationLanguage InterfaceLanguage { get; private set; }

    public ApplicationLanguage? TheologicalLanguage { get; private set; }

    public ComposerEnterBehavior EnterBehavior { get; private set; } =
        DefaultEnterBehavior;

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
            updatedAt);
    }

    public static UserPreferences Create(
        UserId userId,
        ApplicationLanguage interfaceLanguage,
        ApplicationLanguage? theologicalLanguage,
        ComposerEnterBehavior enterBehavior,
        DateTimeOffset updatedAt)
    {
        return new UserPreferences(
            userId,
            interfaceLanguage,
            theologicalLanguage,
            enterBehavior,
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
}
