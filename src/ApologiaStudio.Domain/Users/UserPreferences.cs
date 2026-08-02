namespace ApologiaStudio.Domain.Users;

public sealed class UserPreferences
{
    public const ApplicationLanguage DefaultInterfaceLanguage =
        ApplicationLanguage.French;

    private UserPreferences()
    {
    }

    private UserPreferences(
        UserId userId,
        ApplicationLanguage interfaceLanguage,
        ApplicationLanguage? theologicalLanguage,
        DateTimeOffset updatedAt)
    {
        UserId = userId;
        SetLanguages(
            interfaceLanguage,
            theologicalLanguage);
        UpdatedAt = updatedAt;
    }

    public UserId UserId { get; private set; }

    public ApplicationLanguage InterfaceLanguage { get; private set; }

    public ApplicationLanguage? TheologicalLanguage { get; private set; }

    public ApplicationLanguage EffectiveTheologicalLanguage =>
        TheologicalLanguage ?? InterfaceLanguage;

    public DateTimeOffset UpdatedAt { get; private set; }

    public static UserPreferences Create(
        UserId userId,
        ApplicationLanguage interfaceLanguage,
        ApplicationLanguage? theologicalLanguage,
        DateTimeOffset updatedAt)
    {
        return new UserPreferences(
            userId,
            interfaceLanguage,
            theologicalLanguage,
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
}
