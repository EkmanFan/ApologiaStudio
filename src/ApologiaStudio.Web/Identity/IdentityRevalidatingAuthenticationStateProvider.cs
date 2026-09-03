using System.Security.Claims;
using ApologiaStudio.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ApologiaStudio.Web.Identity;

internal sealed class IdentityRevalidatingAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<IdentityOptions> options)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval =>
        TimeSpan.FromMinutes(5);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApologiaIdentityUser>>();
        var user = await userManager.GetUserAsync(authenticationState.User);
        if (user is null ||
            user.RegistrationStatus != Domain.Users.AccountRegistrationStatus.Active)
        {
            return false;
        }

        if (!userManager.SupportsUserSecurityStamp)
        {
            return true;
        }

        var principalStamp = authenticationState.User.FindFirstValue(
            options.Value.ClaimsIdentity.SecurityStampClaimType);
        var userStamp = await userManager.GetSecurityStampAsync(user);
        return principalStamp == userStamp;
    }
}
