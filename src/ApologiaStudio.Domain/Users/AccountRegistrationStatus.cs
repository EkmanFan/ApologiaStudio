namespace ApologiaStudio.Domain.Users;

public enum AccountRegistrationStatus
{
    PendingEmail = 0,
    PendingApproval = 1,
    Active = 2,
    Rejected = 3,
    Suspended = 4
}
