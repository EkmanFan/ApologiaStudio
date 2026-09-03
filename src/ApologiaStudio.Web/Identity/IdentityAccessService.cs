using ApologiaStudio.Domain.Users;
using ApologiaStudio.Infrastructure.Persistence;
using ApologiaStudio.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Web.Identity;

public sealed class IdentityAccessService(
    ApologiaStudioDbContext database,
    UserManager<ApologiaIdentityUser> userManager)
{
    public async Task<IReadOnlyList<string>> GetEffectiveRolesAsync(
        ApologiaIdentityUser user,
        CancellationToken cancellationToken = default)
    {
        var directRoles = await userManager.GetRolesAsync(user);
        var groupRoles = await (
                from membership in database.IdentityGroupMemberships
                join groupRole in database.IdentityGroupRoles
                    on membership.GroupId equals groupRole.GroupId
                join role in database.Roles
                    on groupRole.RoleId equals role.Id
                where membership.UserId == user.Id && role.Name != null
                select role.Name!)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        return directRoles
            .Concat(groupRoles)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(
        ApologiaIdentityUser user,
        CancellationToken cancellationToken = default)
    {
        var roles = await GetEffectiveRolesAsync(user, cancellationToken);
        return await (
                from role in database.Roles
                join claim in database.RoleClaims on role.Id equals claim.RoleId
                where role.Name != null &&
                      roles.Contains(role.Name) &&
                      claim.ClaimType == SystemPermissions.ClaimType &&
                      claim.ClaimValue != null
                select claim.ClaimValue!)
            .Distinct()
            .Order()
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetGroupsAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await (
                from membership in database.IdentityGroupMemberships
                join groupEntity in database.IdentityGroups
                    on membership.GroupId equals groupEntity.Id
                where membership.UserId == userId
                orderby groupEntity.Name
                select groupEntity.Name)
            .ToArrayAsync(cancellationToken);

    public async Task<bool> HasRoleAsync(
        ApologiaIdentityUser user,
        string role,
        CancellationToken cancellationToken = default) =>
        (await GetEffectiveRolesAsync(user, cancellationToken))
        .Contains(role, StringComparer.Ordinal);

    public async Task<bool> HasPermissionAsync(
        ApologiaIdentityUser user,
        string permission,
        CancellationToken cancellationToken = default) =>
        (await GetEffectivePermissionsAsync(user, cancellationToken))
        .Contains(permission, StringComparer.Ordinal);
}
