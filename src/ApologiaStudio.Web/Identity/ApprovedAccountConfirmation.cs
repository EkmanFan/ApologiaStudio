using ApologiaStudio.Domain.Users;
using ApologiaStudio.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Identity;

namespace ApologiaStudio.Web.Identity;

public sealed class ApprovedAccountConfirmation
    : IUserConfirmation<ApologiaIdentityUser>
{
    public Task<bool> IsConfirmedAsync(
        UserManager<ApologiaIdentityUser> manager,
        ApologiaIdentityUser user) =>
        Task.FromResult(
            user.EmailConfirmed &&
            user.RegistrationStatus == AccountRegistrationStatus.Active);
}
