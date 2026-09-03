namespace ApologiaStudio.Domain.Users;

public static class SystemRoles
{
    public const string Reader = "Reader";
    public const string Editor = "Editor";
    public const string DocumentOperator = "DocumentOperator";
    public const string Administrator = "Administrator";

    public static IReadOnlyList<string> All { get; } =
    [
        Reader,
        Editor,
        DocumentOperator,
        Administrator
    ];
}

public static class SystemGroups
{
    public const string Readers = "Readers";
    public const string Editors = "Editors";
    public const string DocumentOperators = "Document Operators";
    public const string Administrators = "Administrators";

    public static IReadOnlyDictionary<string, string> RoleByGroup { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Readers] = SystemRoles.Reader,
            [Editors] = SystemRoles.Editor,
            [DocumentOperators] = SystemRoles.DocumentOperator,
            [Administrators] = SystemRoles.Administrator
        };
}

public static class SystemPermissions
{
    public const string ClaimType = "apologia.permission";
    public const string AccessStudio = "studio.access";
    public const string ManageAccounts = "identity.accounts.manage";
    public const string ManageGroups = "identity.groups.manage";
    public const string ManageRoles = "identity.roles.manage";
    public const string ReviewEditorial = "editorial.review";
    public const string PurgeEditorial = "editorial.purge";
    public const string OperateDocumentManager = "manager.operate";
    public const string ReplayDocumentDelivery = "manager.delivery.replay";
    public const string PurgeManagerCustody = "manager.custody.purge";
    public const string ManageSettings = "settings.manage";

    public static IReadOnlyList<string> All { get; } =
    [
        AccessStudio,
        ManageAccounts,
        ManageGroups,
        ManageRoles,
        ReviewEditorial,
        PurgeEditorial,
        OperateDocumentManager,
        ReplayDocumentDelivery,
        PurgeManagerCustody,
        ManageSettings
    ];
}

public static class SystemPolicies
{
    public const string ManageAccess = "identity.access.administration";
    public const string ViewIdentityAdministration =
        "identity.administration.identity.view";
    public const string ViewAdministration = "identity.administration.view";
}
