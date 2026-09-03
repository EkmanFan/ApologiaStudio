using System.Security.Claims;
using ApologiaStudio.Domain.Users;
using ApologiaStudio.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ApologiaStudio.Web.Identity;

public sealed class ApologiaUserClaimsPrincipalFactory(
    UserManager<ApologiaIdentityUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IdentityAccessService accessService,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApologiaIdentityUser, IdentityRole<Guid>>(
        userManager,
        roleManager,
        options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(
        ApologiaIdentityUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        var currentNameClaim = identity.FindFirst(identity.NameClaimType);
        if (currentNameClaim is not null)
        {
            identity.RemoveClaim(currentNameClaim);
        }

        var displayName = string.IsNullOrWhiteSpace(user.DisplayName)
            ? user.Email ?? user.UserName ?? "Account"
            : user.DisplayName.Trim();
        identity.AddClaim(
            new Claim(identity.NameClaimType, displayName));

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            AddIfMissing(
                identity,
                new Claim(ClaimTypes.Email, user.Email.Trim()));
        }

        var groups = await accessService.GetGroupsAsync(user.Id);
        var roles = await accessService.GetEffectiveRolesAsync(user);
        var permissions = await accessService.GetEffectivePermissionsAsync(user);

        foreach (var group in groups)
        {
            AddIfMissing(identity, new Claim("apologia.group", group));
        }

        foreach (var role in roles)
        {
            AddIfMissing(identity, new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in permissions)
        {
            AddIfMissing(
                identity,
                new Claim(SystemPermissions.ClaimType, permission));
        }

        return identity;
    }

    private static void AddIfMissing(
        ClaimsIdentity identity,
        Claim claim)
    {
        if (!identity.HasClaim(claim.Type, claim.Value))
        {
            identity.AddClaim(claim);
        }
    }
}
