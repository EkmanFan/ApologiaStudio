using System.Text.Json;
using ApologiaStudio.Domain.Users;
using ApologiaStudio.Infrastructure.Persistence;
using ApologiaStudio.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Web.Identity;

public sealed record AccessMemberView(
    Guid Id,
    string DisplayName,
    string Email,
    AccountRegistrationStatus Status);

public sealed record AccessGroupView(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    IReadOnlyList<Guid> RoleIds,
    IReadOnlyList<Guid> MemberIds);

public sealed record AccessRoleView(
    Guid Id,
    string Name,
    bool IsSystem,
    IReadOnlyList<string> Permissions,
    int GroupCount);

public sealed record AccessAuditEventView(
    long Id,
    DateTimeOffset OccurredAtUtc,
    string Actor,
    string Action,
    string Target,
    string? Reason);

public sealed record AccessAdministrationSnapshot(
    IReadOnlyList<AccessMemberView> Members,
    IReadOnlyList<AccessGroupView> Groups,
    IReadOnlyList<AccessRoleView> Roles,
    IReadOnlyList<string> PermissionCatalog,
    IReadOnlyList<AccessAuditEventView> RecentEvents);

public sealed class AccessAdministrationService(
    ApologiaStudioDbContext database,
    UserManager<ApologiaIdentityUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IdentityAccessService accessService,
    TimeProvider timeProvider)
{
    public async Task<AccessAdministrationSnapshot> GetSnapshotAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await EnsureAnyPermissionAsync(
            actorUserId,
            [SystemPermissions.ManageGroups, SystemPermissions.ManageRoles],
            cancellationToken);

        var members = await userManager.Users
            .Where(user =>
                user.RegistrationStatus == AccountRegistrationStatus.Active ||
                user.RegistrationStatus == AccountRegistrationStatus.Suspended)
            .OrderBy(user => user.DisplayName)
            .Select(user => new AccessMemberView(
                user.Id,
                user.DisplayName,
                user.Email ?? string.Empty,
                user.RegistrationStatus))
            .ToArrayAsync(cancellationToken);
        var groups = await database.IdentityGroups
            .OrderByDescending(group => group.IsSystem)
            .ThenBy(group => group.Name)
            .ToArrayAsync(cancellationToken);
        var roles = await database.Roles
            .Where(role => role.Name != null)
            .OrderBy(role => role.Name)
            .ToArrayAsync(cancellationToken);
        var memberships = await database.IdentityGroupMemberships
            .ToArrayAsync(cancellationToken);
        var groupRoles = await database.IdentityGroupRoles
            .ToArrayAsync(cancellationToken);
        var permissionClaims = await database.RoleClaims
            .Where(claim =>
                claim.ClaimType == SystemPermissions.ClaimType &&
                claim.ClaimValue != null)
            .ToArrayAsync(cancellationToken);
        var events = await database.IdentityAdministrationEvents
            .OrderByDescending(item => item.OccurredAtUtc)
            .Take(100)
            .ToArrayAsync(cancellationToken);
        var memberNames = members.ToDictionary(
            member => member.Id,
            member => member.DisplayName);
        var groupNames = groups.ToDictionary(group => group.Id, group => group.Name);
        var roleNames = roles.ToDictionary(role => role.Id, role => role.Name!);

        return new AccessAdministrationSnapshot(
            members,
            groups.Select(group => new AccessGroupView(
                    group.Id,
                    group.Name,
                    group.Description,
                    group.IsSystem,
                    groupRoles
                        .Where(item => item.GroupId == group.Id)
                        .Select(item => item.RoleId)
                        .Order()
                        .ToArray(),
                    memberships
                        .Where(item => item.GroupId == group.Id)
                        .Select(item => item.UserId)
                        .Order()
                        .ToArray()))
                .ToArray(),
            roles.Select(role => new AccessRoleView(
                    role.Id,
                    role.Name!,
                    SystemRoles.All.Contains(role.Name!, StringComparer.Ordinal),
                    permissionClaims
                        .Where(claim => claim.RoleId == role.Id)
                        .Select(claim => claim.ClaimValue!)
                        .Order()
                        .ToArray(),
                    groupRoles.Count(item => item.RoleId == role.Id)))
                .ToArray(),
            SystemPermissions.All.Order().ToArray(),
            events.Select(item => new AccessAuditEventView(
                    item.Id,
                    item.OccurredAtUtc,
                    memberNames.GetValueOrDefault(item.ActorUserId, "Compte supprimé"),
                    item.Action,
                    ResolveTarget(item, memberNames, groupNames, roleNames),
                    item.Reason))
                .ToArray());
    }

    public async Task<Guid> CreateGroupAsync(
        Guid actorUserId,
        string name,
        string? description,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(
            actorUserId,
            SystemPermissions.ManageGroups,
            cancellationToken);
        name = ValidateName(name, "Le nom du groupe");
        description = ValidateDescription(description);
        var normalizedName = IdentityBootstrapper.NormalizeGroupName(name);
        if (await database.IdentityGroups.AnyAsync(
                group => group.NormalizedName == normalizedName,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "Un groupe porte déjà ce nom.");
        }

        var now = timeProvider.GetUtcNow();
        var group = new IdentityGroupEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            NormalizedName = normalizedName,
            Description = description,
            CreatedAtUtc = now,
            CreatedByUserId = actorUserId
        };
        database.IdentityGroups.Add(group);
        AddEvent(actorUserId, targetGroupId: group.Id, action: "group.create");
        await database.SaveChangesAsync(cancellationToken);
        return group.Id;
    }

    public async Task UpdateGroupAsync(
        Guid actorUserId,
        Guid groupId,
        string name,
        string? description,
        IReadOnlyCollection<Guid> roleIds,
        IReadOnlyCollection<Guid> memberIds,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(
            actorUserId,
            SystemPermissions.ManageGroups,
            cancellationToken);
        var group = await database.IdentityGroups.SingleOrDefaultAsync(
            candidate => candidate.Id == groupId,
            cancellationToken) ?? throw new InvalidOperationException(
                "Le groupe demandé n’existe plus.");

        name = ValidateName(name, "Le nom du groupe");
        description = ValidateDescription(description);
        var normalizedName = IdentityBootstrapper.NormalizeGroupName(name);
        if (await database.IdentityGroups.AnyAsync(
                candidate => candidate.Id != groupId &&
                             candidate.NormalizedName == normalizedName,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "Un groupe porte déjà ce nom.");
        }

        var distinctRoleIds = roleIds.Distinct().ToArray();
        var distinctMemberIds = memberIds.Distinct().ToArray();
        var validRoleIds = await database.Roles
            .Where(role => distinctRoleIds.Contains(role.Id))
            .Select(role => role.Id)
            .ToArrayAsync(cancellationToken);
        if (validRoleIds.Length != distinctRoleIds.Length)
        {
            throw new InvalidOperationException("Un rôle sélectionné n’existe plus.");
        }

        var validMemberIds = await userManager.Users
            .Where(user =>
                distinctMemberIds.Contains(user.Id) &&
                (user.RegistrationStatus == AccountRegistrationStatus.Active ||
                 user.RegistrationStatus == AccountRegistrationStatus.Suspended))
            .Select(user => user.Id)
            .ToArrayAsync(cancellationToken);
        if (validMemberIds.Length != distinctMemberIds.Length)
        {
            throw new InvalidOperationException("Un compte sélectionné n’existe plus.");
        }

        if (group.IsSystem)
        {
            var requiredRoleName = SystemGroups.RoleByGroup[group.Name];
            var requiredRoleId = await database.Roles
                .Where(role => role.Name == requiredRoleName)
                .Select(role => role.Id)
                .SingleAsync(cancellationToken);
            if (distinctRoleIds.Length != 1 || distinctRoleIds[0] != requiredRoleId)
            {
                throw new InvalidOperationException(
                    "Le rôle d’un groupe système ne peut pas être modifié.");
            }

            name = group.Name;
            normalizedName = group.NormalizedName;
        }

        var currentMemberships = await database.IdentityGroupMemberships
            .Where(membership => membership.GroupId == groupId)
            .ToArrayAsync(cancellationToken);
        var currentRoleAssignments = await database.IdentityGroupRoles
            .Where(assignment => assignment.GroupId == groupId)
            .ToArrayAsync(cancellationToken);
        var administratorRoleId = await database.Roles
            .Where(role => role.Name == SystemRoles.Administrator)
            .Select(role => role.Id)
            .SingleAsync(cancellationToken);
        var removedMemberIds = currentMemberships
            .Where(item => !distinctMemberIds.Contains(item.UserId))
            .Select(item => item.UserId)
            .ToArray();
        var currentlyGrantsAdministrator = currentRoleAssignments.Any(
            assignment => assignment.RoleId == administratorRoleId);
        if (currentlyGrantsAdministrator)
        {
            var administratorRoleRemoved = !distinctRoleIds.Contains(
                administratorRoleId);
            var affectedUserIds = administratorRoleRemoved
                ? currentMemberships.Select(item => item.UserId)
                : removedMemberIds;
            foreach (var removedUserId in affectedUserIds.Distinct())
            {
                await EnsureAdministratorWouldRemainAsync(
                    removedUserId,
                    groupId,
                    cancellationToken);
            }
        }

        await using var transaction = await database.Database
            .BeginTransactionAsync(cancellationToken);
        group.Name = name;
        group.NormalizedName = normalizedName;
        group.Description = description;

        database.IdentityGroupMemberships.RemoveRange(
            currentMemberships.Where(item =>
                !distinctMemberIds.Contains(item.UserId)));
        var currentMemberIds = currentMemberships
            .Select(item => item.UserId)
            .ToHashSet();
        var now = timeProvider.GetUtcNow();
        foreach (var userId in distinctMemberIds.Where(id =>
                     !currentMemberIds.Contains(id)))
        {
            database.IdentityGroupMemberships.Add(
                new IdentityGroupMembershipEntity
                {
                    GroupId = groupId,
                    UserId = userId,
                    AddedByUserId = actorUserId,
                    AddedAtUtc = now
                });
        }

        database.IdentityGroupRoles.RemoveRange(
            currentRoleAssignments.Where(item =>
                !distinctRoleIds.Contains(item.RoleId)));
        var currentRoleIds = currentRoleAssignments
            .Select(item => item.RoleId)
            .ToHashSet();
        foreach (var roleId in distinctRoleIds.Where(id =>
                     !currentRoleIds.Contains(id)))
        {
            database.IdentityGroupRoles.Add(
                new IdentityGroupRoleEntity
                {
                    GroupId = groupId,
                    RoleId = roleId,
                    AssignedByUserId = actorUserId,
                    AssignedAtUtc = now
                });
        }

        AddEvent(
            actorUserId,
            targetGroupId: groupId,
            action: "group.update",
            details: JsonSerializer.Serialize(new
            {
                Roles = distinctRoleIds,
                Members = distinctMemberIds
            }));
        await database.SaveChangesAsync(cancellationToken);
        await UpdateSecurityStampsAsync(
            currentMemberIds.Concat(distinctMemberIds).Distinct(),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteGroupAsync(
        Guid actorUserId,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(
            actorUserId,
            SystemPermissions.ManageGroups,
            cancellationToken);
        var group = await database.IdentityGroups.SingleOrDefaultAsync(
            candidate => candidate.Id == groupId,
            cancellationToken) ?? throw new InvalidOperationException(
                "Le groupe demandé n’existe plus.");
        if (group.IsSystem)
        {
            throw new InvalidOperationException(
                "Un groupe système ne peut pas être supprimé.");
        }

        var memberIds = await database.IdentityGroupMemberships
            .Where(membership => membership.GroupId == groupId)
            .Select(membership => membership.UserId)
            .ToArrayAsync(cancellationToken);
        if (await GroupCurrentlyGrantsAdministratorAsync(
                groupId,
                cancellationToken))
        {
            foreach (var userId in memberIds)
            {
                await EnsureAdministratorWouldRemainAsync(
                    userId,
                    groupId,
                    cancellationToken);
            }
        }

        database.IdentityGroups.Remove(group);
        AddEvent(
            actorUserId,
            action: "group.delete",
            details: group.Name);
        await database.SaveChangesAsync(cancellationToken);
        await UpdateSecurityStampsAsync(memberIds, cancellationToken);
    }

    public async Task<Guid> CreateRoleAsync(
        Guid actorUserId,
        string name,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(
            actorUserId,
            SystemPermissions.ManageRoles,
            cancellationToken);
        name = ValidateName(name, "Le nom du rôle");
        var role = new IdentityRole<Guid>(name) { Id = Guid.NewGuid() };
        await using var transaction = await database.Database
            .BeginTransactionAsync(cancellationToken);
        EnsureSucceeded(await roleManager.CreateAsync(role), "créer le rôle");
        AddEvent(actorUserId, targetRoleId: role.Id, action: "role.create");
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return role.Id;
    }

    public async Task UpdateRolePermissionsAsync(
        Guid actorUserId,
        Guid roleId,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(
            actorUserId,
            SystemPermissions.ManageRoles,
            cancellationToken);
        var role = await roleManager.FindByIdAsync(roleId.ToString())
            ?? throw new InvalidOperationException("Le rôle demandé n’existe plus.");
        if (string.Equals(
                role.Name,
                SystemRoles.Administrator,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Le rôle Administrator conserve toujours tous les droits.");
        }

        var distinctPermissions = permissions
            .Distinct(StringComparer.Ordinal)
            .Order()
            .ToArray();
        if (distinctPermissions.Any(permission =>
                !SystemPermissions.All.Contains(
                    permission,
                    StringComparer.Ordinal)))
        {
            throw new InvalidOperationException(
                "Une permission sélectionnée ne fait pas partie du catalogue.");
        }

        await using var transaction = await database.Database
            .BeginTransactionAsync(cancellationToken);
        var currentClaims = (await roleManager.GetClaimsAsync(role))
            .Where(claim => claim.Type == SystemPermissions.ClaimType)
            .ToArray();
        foreach (var claim in currentClaims.Where(claim =>
                     !distinctPermissions.Contains(
                         claim.Value,
                         StringComparer.Ordinal)))
        {
            EnsureSucceeded(
                await roleManager.RemoveClaimAsync(role, claim),
                "retirer le droit du rôle");
        }

        foreach (var permission in distinctPermissions.Where(permission =>
                     currentClaims.All(claim => claim.Value != permission)))
        {
            EnsureSucceeded(
                await roleManager.AddClaimAsync(
                    role,
                    new System.Security.Claims.Claim(
                        SystemPermissions.ClaimType,
                        permission)),
                "ajouter le droit au rôle");
        }

        AddEvent(
            actorUserId,
            targetRoleId: roleId,
            action: "role.permissions.update",
            details: JsonSerializer.Serialize(distinctPermissions));
        await database.SaveChangesAsync(cancellationToken);
        await UpdateSecurityStampsForRoleAsync(roleId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteRoleAsync(
        Guid actorUserId,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(
            actorUserId,
            SystemPermissions.ManageRoles,
            cancellationToken);
        var role = await roleManager.FindByIdAsync(roleId.ToString())
            ?? throw new InvalidOperationException("Le rôle demandé n’existe plus.");
        if (role.Name is not null &&
            SystemRoles.All.Contains(role.Name, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Un rôle système ne peut pas être supprimé.");
        }

        if (await database.IdentityGroupRoles.AnyAsync(
                assignment => assignment.RoleId == roleId,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "Retirez ce rôle de tous les groupes avant de le supprimer.");
        }

        var roleName = role.Name;
        await using var transaction = await database.Database
            .BeginTransactionAsync(cancellationToken);
        EnsureSucceeded(await roleManager.DeleteAsync(role), "supprimer le rôle");
        AddEvent(
            actorUserId,
            action: "role.delete",
            details: roleName);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task EnsurePermissionAsync(
        Guid actorUserId,
        string permission,
        CancellationToken cancellationToken)
    {
        var actor = await userManager.FindByIdAsync(actorUserId.ToString());
        if (actor is null ||
            actor.RegistrationStatus != AccountRegistrationStatus.Active ||
            !await accessService.HasPermissionAsync(
                actor,
                permission,
                cancellationToken))
        {
            throw new UnauthorizedAccessException(
                "Cette action n’est pas autorisée pour ce compte.");
        }
    }

    private async Task EnsureAnyPermissionAsync(
        Guid actorUserId,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken)
    {
        var actor = await userManager.FindByIdAsync(actorUserId.ToString());
        if (actor is null ||
            actor.RegistrationStatus != AccountRegistrationStatus.Active)
        {
            throw new UnauthorizedAccessException(
                "Cette action n’est pas autorisée pour ce compte.");
        }

        var effectivePermissions = await accessService
            .GetEffectivePermissionsAsync(actor, cancellationToken);
        if (!permissions.Any(permission =>
                effectivePermissions.Contains(permission, StringComparer.Ordinal)))
        {
            throw new UnauthorizedAccessException(
                "Cette action n’est pas autorisée pour ce compte.");
        }
    }

    private async Task<bool> GroupCurrentlyGrantsAdministratorAsync(
        Guid groupId,
        CancellationToken cancellationToken) =>
        await (
                from assignment in database.IdentityGroupRoles
                join role in database.Roles on assignment.RoleId equals role.Id
                where assignment.GroupId == groupId &&
                      role.Name == SystemRoles.Administrator
                select assignment)
            .AnyAsync(cancellationToken);

    private async Task EnsureAdministratorWouldRemainAsync(
        Guid removedUserId,
        Guid removedGroupId,
        CancellationToken cancellationToken)
    {
        var removedUser = await userManager.FindByIdAsync(
            removedUserId.ToString());
        if (removedUser is null ||
            removedUser.RegistrationStatus != AccountRegistrationStatus.Active ||
            !await accessService.HasRoleAsync(
                removedUser,
                SystemRoles.Administrator,
                cancellationToken))
        {
            return;
        }

        var hasDirectRole = await userManager.IsInRoleAsync(
            removedUser,
            SystemRoles.Administrator);
        var hasOtherGroupRole = await (
                from membership in database.IdentityGroupMemberships
                join assignment in database.IdentityGroupRoles
                    on membership.GroupId equals assignment.GroupId
                join role in database.Roles on assignment.RoleId equals role.Id
                where membership.UserId == removedUserId &&
                      membership.GroupId != removedGroupId &&
                      role.Name == SystemRoles.Administrator
                select membership)
            .AnyAsync(cancellationToken);
        if (hasDirectRole || hasOtherGroupRole)
        {
            return;
        }

        var otherActiveUsers = await userManager.Users
            .Where(user => user.Id != removedUserId &&
                           user.RegistrationStatus ==
                           AccountRegistrationStatus.Active)
            .ToArrayAsync(cancellationToken);
        foreach (var user in otherActiveUsers)
        {
            if (await accessService.HasRoleAsync(
                    user,
                    SystemRoles.Administrator,
                    cancellationToken))
            {
                return;
            }
        }

        throw new InvalidOperationException(
            "Le dernier administrateur actif doit rester membre d’un groupe administrateur.");
    }

    private async Task UpdateSecurityStampsForRoleAsync(
        Guid roleId,
        CancellationToken cancellationToken)
    {
        var userIds = await database.UserRoles
            .Where(item => item.RoleId == roleId)
            .Select(item => item.UserId)
            .Concat(
                from membership in database.IdentityGroupMemberships
                join assignment in database.IdentityGroupRoles
                    on membership.GroupId equals assignment.GroupId
                where assignment.RoleId == roleId
                select membership.UserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        await UpdateSecurityStampsAsync(userIds, cancellationToken);
    }

    private async Task UpdateSecurityStampsAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        foreach (var userId in userIds.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is not null)
            {
                EnsureSucceeded(
                    await userManager.UpdateSecurityStampAsync(user),
                    "révoquer la session après le changement de droits");
            }
        }
    }

    private void AddEvent(
        Guid actorUserId,
        string action,
        Guid? targetUserId = null,
        Guid? targetGroupId = null,
        Guid? targetRoleId = null,
        string? reason = null,
        string? details = null) =>
        database.IdentityAdministrationEvents.Add(
            new IdentityAdministrationEventEntity
            {
                ActorUserId = actorUserId,
                TargetUserId = targetUserId,
                TargetGroupId = targetGroupId,
                TargetRoleId = targetRoleId,
                Action = action,
                Reason = reason,
                Details = details,
                OccurredAtUtc = timeProvider.GetUtcNow()
            });

    private static string ValidateName(string name, string label)
    {
        name = name.Trim();
        if (name.Length is < 2 or > 120)
        {
            throw new InvalidOperationException(
                $"{label} doit contenir entre 2 et 120 caractères.");
        }

        return name;
    }

    private static string? ValidateDescription(string? description)
    {
        description = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();
        if (description?.Length > 500)
        {
            throw new InvalidOperationException(
                "La description ne peut pas dépasser 500 caractères.");
        }

        return description;
    }

    private static void EnsureSucceeded(
        IdentityResult result,
        string operation)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Impossible de {operation} : " +
                string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }

    private static string ResolveTarget(
        IdentityAdministrationEventEntity item,
        IReadOnlyDictionary<Guid, string> members,
        IReadOnlyDictionary<Guid, string> groups,
        IReadOnlyDictionary<Guid, string> roles)
    {
        if (item.TargetUserId is { } userId)
        {
            return members.GetValueOrDefault(userId, "Compte supprimé");
        }

        if (item.TargetGroupId is { } groupId)
        {
            return groups.GetValueOrDefault(groupId, "Groupe supprimé");
        }

        if (item.TargetRoleId is { } roleId)
        {
            return roles.GetValueOrDefault(roleId, "Rôle supprimé");
        }

        return item.Details ?? "—";
    }
}
