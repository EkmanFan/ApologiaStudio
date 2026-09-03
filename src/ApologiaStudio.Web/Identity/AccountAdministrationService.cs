using System.Text.Json;
using ApologiaStudio.Domain.Users;
using ApologiaStudio.Infrastructure.Persistence;
using ApologiaStudio.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Web.Identity;

public sealed record AccountAdministrationView(
    Guid Id,
    string Email,
    string DisplayName,
    AccountRegistrationStatus Status,
    DateTimeOffset RegisteredAtUtc,
    DateTimeOffset? EmailVerifiedAtUtc,
    DateTimeOffset? ReviewedAtUtc,
    string? RejectionReason,
    DateTimeOffset? LockoutEnd,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Groups);

public sealed record CreatedAccountResult(
    ApologiaIdentityUser User,
    string? EmailConfirmationToken);

public sealed record AccountGroupAssignmentView(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    bool IsSelected,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

public sealed record AccountAccessDetailsView(
    Guid Id,
    string Email,
    string DisplayName,
    AccountRegistrationStatus Status,
    IReadOnlyList<AccountGroupAssignmentView> Groups,
    IReadOnlyList<string> EffectiveRoles,
    IReadOnlyList<string> EffectivePermissions);

public sealed class AccountAdministrationService(
    UserManager<ApologiaIdentityUser> userManager,
    ApologiaStudioDbContext database,
    IdentityAccessService accessService,
    TimeProvider timeProvider)
{
    public async Task<AccountAccessDetailsView> GetAccessDetailsAsync(
        Guid targetUserId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await EnsureAdministratorAsync(actorUserId);
        var user = await RequireUserAsync(targetUserId);
        var groups = await database.IdentityGroups
            .OrderByDescending(group => group.IsSystem)
            .ThenBy(group => group.Name)
            .ToArrayAsync(cancellationToken);
        var selectedGroupIds = await database.IdentityGroupMemberships
            .Where(item => item.UserId == targetUserId)
            .Select(item => item.GroupId)
            .ToHashSetAsync(cancellationToken);
        var groupRoles = await (
                from assignment in database.IdentityGroupRoles
                join role in database.Roles on assignment.RoleId equals role.Id
                where role.Name != null
                select new
                {
                    assignment.GroupId,
                    assignment.RoleId,
                    RoleName = role.Name!
                })
            .ToArrayAsync(cancellationToken);
        var rolePermissions = await database.RoleClaims
            .Where(claim =>
                claim.ClaimType == SystemPermissions.ClaimType &&
                claim.ClaimValue != null)
            .Select(claim => new
            {
                claim.RoleId,
                Permission = claim.ClaimValue!
            })
            .ToArrayAsync(cancellationToken);

        return new AccountAccessDetailsView(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            user.RegistrationStatus,
            groups.Select(group =>
                {
                    var roles = groupRoles
                        .Where(item => item.GroupId == group.Id)
                        .ToArray();
                    var roleIds = roles.Select(item => item.RoleId).ToHashSet();
                    return new AccountGroupAssignmentView(
                        group.Id,
                        group.Name,
                        group.Description,
                        group.IsSystem,
                        selectedGroupIds.Contains(group.Id),
                        roles.Select(item => item.RoleName)
                            .Distinct(StringComparer.Ordinal)
                            .Order(StringComparer.Ordinal)
                            .ToArray(),
                        rolePermissions
                            .Where(item => roleIds.Contains(item.RoleId))
                            .Select(item => item.Permission)
                            .Distinct(StringComparer.Ordinal)
                            .Order(StringComparer.Ordinal)
                            .ToArray());
                })
                .ToArray(),
            await accessService.GetEffectiveRolesAsync(user, cancellationToken),
            await accessService.GetEffectivePermissionsAsync(user, cancellationToken));
    }

    public async Task UpdateGroupsAsync(
        Guid targetUserId,
        Guid actorUserId,
        IReadOnlyCollection<Guid> groupIds,
        CancellationToken cancellationToken)
    {
        await EnsureAdministratorAsync(actorUserId);
        var user = await RequireUserAsync(targetUserId);
        if (user.RegistrationStatus is not (
            AccountRegistrationStatus.Active or
            AccountRegistrationStatus.Suspended))
        {
            throw new InvalidOperationException(
                "Les groupes ne peuvent être attribués qu’à un compte actif ou suspendu.");
        }

        var selectedGroupIds = groupIds.Distinct().ToArray();
        var validGroupIds = await database.IdentityGroups
            .Where(group => selectedGroupIds.Contains(group.Id))
            .Select(group => group.Id)
            .ToArrayAsync(cancellationToken);
        if (validGroupIds.Length != selectedGroupIds.Length)
        {
            throw new InvalidOperationException(
                "Un groupe sélectionné n’existe plus.");
        }

        var currentlyAdministrator =
            user.RegistrationStatus == AccountRegistrationStatus.Active &&
            await accessService.HasRoleAsync(
                user,
                SystemRoles.Administrator,
                cancellationToken);
        if (currentlyAdministrator)
        {
            var administratorGroupIds = await (
                    from assignment in database.IdentityGroupRoles
                    join role in database.Roles on assignment.RoleId equals role.Id
                    where role.Name == SystemRoles.Administrator
                    select assignment.GroupId)
                .Distinct()
                .ToArrayAsync(cancellationToken);
            var keepsAdministratorRole =
                await userManager.IsInRoleAsync(user, SystemRoles.Administrator) ||
                selectedGroupIds.Intersect(administratorGroupIds).Any();
            if (!keepsAdministratorRole)
            {
                var otherActiveUsers = await userManager.Users
                    .Where(candidate =>
                        candidate.Id != user.Id &&
                        candidate.RegistrationStatus == AccountRegistrationStatus.Active)
                    .ToArrayAsync(cancellationToken);
                var anotherAdministratorExists = false;
                foreach (var candidate in otherActiveUsers)
                {
                    if (await accessService.HasRoleAsync(
                            candidate,
                            SystemRoles.Administrator,
                            cancellationToken))
                    {
                        anotherAdministratorExists = true;
                        break;
                    }
                }

                if (!anotherAdministratorExists)
                {
                    throw new InvalidOperationException(
                        "Le dernier administrateur actif doit rester membre d’un groupe administrateur.");
                }
            }
        }

        var memberships = await database.IdentityGroupMemberships
            .Where(item => item.UserId == targetUserId)
            .ToArrayAsync(cancellationToken);
        var currentGroupIds = memberships.Select(item => item.GroupId).ToHashSet();
        await using var transaction = await database.Database
            .BeginTransactionAsync(cancellationToken);
        database.IdentityGroupMemberships.RemoveRange(
            memberships.Where(item => !selectedGroupIds.Contains(item.GroupId)));
        var now = timeProvider.GetUtcNow();
        foreach (var groupId in selectedGroupIds.Where(id => !currentGroupIds.Contains(id)))
        {
            database.IdentityGroupMemberships.Add(
                new IdentityGroupMembershipEntity
                {
                    GroupId = groupId,
                    UserId = targetUserId,
                    AddedByUserId = actorUserId,
                    AddedAtUtc = now
                });
        }

        database.IdentityAdministrationEvents.Add(
            new IdentityAdministrationEventEntity
            {
                TargetUserId = targetUserId,
                ActorUserId = actorUserId,
                Action = "account.groups.update",
                Details = JsonSerializer.Serialize(new
                {
                    PreviousGroups = currentGroupIds.Order().ToArray(),
                    Groups = selectedGroupIds.Order().ToArray()
                }),
                OccurredAtUtc = now
            });
        await database.SaveChangesAsync(cancellationToken);
        EnsureSucceeded(
            await userManager.UpdateSecurityStampAsync(user),
            "révoquer la session après le changement de droits");
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<CreatedAccountResult> CreateAsync(
        Guid actorUserId,
        string displayName,
        string email,
        string password,
        bool bypassEmailVerification,
        CancellationToken cancellationToken)
    {
        await EnsureAdministratorAsync(actorUserId);

        displayName = displayName.Trim();
        email = email.Trim();
        if (displayName.Length is < 2 or > 200)
        {
            throw new InvalidOperationException(
                "Le nom affiché doit contenir entre 2 et 200 caractères.");
        }

        var now = timeProvider.GetUtcNow();
        var user = new ApologiaIdentityUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = displayName,
            EmailConfirmed = bypassEmailVerification,
            EmailVerifiedAtUtc = bypassEmailVerification ? now : null,
            RegistrationStatus = bypassEmailVerification
                ? AccountRegistrationStatus.Active
                : AccountRegistrationStatus.PendingEmail,
            RegisteredAtUtc = now,
            ReviewedAtUtc = bypassEmailVerification ? now : null,
            ReviewedByUserId = bypassEmailVerification ? actorUserId : null,
            LockoutEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };

        await using var transaction = await database.Database
            .BeginTransactionAsync(cancellationToken);
        EnsureSucceeded(
            await userManager.CreateAsync(user, password),
            "créer le compte");

        if (bypassEmailVerification)
        {
            var normalizedName = IdentityBootstrapper.NormalizeGroupName(
                SystemGroups.Readers);
            var readerGroup = await database.IdentityGroups.SingleAsync(
                candidate => candidate.NormalizedName == normalizedName,
                cancellationToken);
            database.IdentityGroupMemberships.Add(
                new IdentityGroupMembershipEntity
                {
                    GroupId = readerGroup.Id,
                    UserId = user.Id,
                    AddedByUserId = actorUserId,
                    AddedAtUtc = now
                });
        }

        database.IdentityAdministrationEvents.Add(
            new IdentityAdministrationEventEntity
            {
                TargetUserId = user.Id,
                ActorUserId = actorUserId,
                Action = bypassEmailVerification
                    ? "account.create.verified"
                    : "account.create",
                Reason = bypassEmailVerification
                    ? "email-verification-bypassed"
                    : null,
                OccurredAtUtc = now
            });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var confirmationToken = bypassEmailVerification
            ? null
            : await userManager.GenerateEmailConfirmationTokenAsync(user);
        return new CreatedAccountResult(user, confirmationToken);
    }

    public async Task<IReadOnlyList<AccountAdministrationView>> ListAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await EnsureAdministratorAsync(actorUserId);
        var users = await userManager.Users
            .OrderBy(user => user.RegisteredAtUtc)
            .ToArrayAsync(cancellationToken);
        var views = new List<AccountAdministrationView>(users.Length);

        foreach (var user in users)
        {
            views.Add(await ToViewAsync(user));
        }

        return views;
    }

    public Task ApproveAsync(
        Guid targetUserId,
        Guid actorUserId,
        CancellationToken cancellationToken) =>
        ChangeAsync(
            targetUserId,
            actorUserId,
            AccountRegistrationStatus.PendingApproval,
            AccountRegistrationStatus.Active,
            "approve",
            null,
            assignReaderGroup: true,
            cancellationToken);

    public Task RejectAsync(
        Guid targetUserId,
        Guid actorUserId,
        string reason,
        CancellationToken cancellationToken)
    {
        reason = reason.Trim();
        if (reason.Length is < 3 or > 2000)
        {
            throw new InvalidOperationException(
                "Le motif du refus doit contenir entre 3 et 2000 caractères.");
        }

        return ChangeAsync(
            targetUserId,
            actorUserId,
            AccountRegistrationStatus.PendingApproval,
            AccountRegistrationStatus.Rejected,
            "reject",
            reason,
            assignReaderGroup: false,
            cancellationToken);
    }

    public Task SuspendAsync(
        Guid targetUserId,
        Guid actorUserId,
        string reason,
        CancellationToken cancellationToken)
    {
        reason = reason.Trim();
        if (reason.Length is < 3 or > 2000)
        {
            throw new InvalidOperationException(
                "Le motif de suspension doit contenir entre 3 et 2000 caractères.");
        }

        return ChangeAsync(
            targetUserId,
            actorUserId,
            AccountRegistrationStatus.Active,
            AccountRegistrationStatus.Suspended,
            "suspend",
            reason,
            assignReaderGroup: false,
            cancellationToken);
    }

    public Task ReactivateAsync(
        Guid targetUserId,
        Guid actorUserId,
        CancellationToken cancellationToken) =>
        ChangeAsync(
            targetUserId,
            actorUserId,
            AccountRegistrationStatus.Suspended,
            AccountRegistrationStatus.Active,
            "reactivate",
            null,
            assignReaderGroup: false,
            cancellationToken);

    public async Task UnlockAsync(
        Guid targetUserId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await EnsureAdministratorAsync(actorUserId);
        var user = await RequireUserAsync(targetUserId);
        EnsureSucceeded(
            await userManager.SetLockoutEndDateAsync(user, null),
            "déverrouiller le compte");
        EnsureSucceeded(
            await userManager.ResetAccessFailedCountAsync(user),
            "réinitialiser les tentatives de connexion");
        await AddEventAsync(
            user.Id,
            actorUserId,
            "unlock",
            null,
            cancellationToken);
    }

    private async Task ChangeAsync(
        Guid targetUserId,
        Guid actorUserId,
        AccountRegistrationStatus expectedStatus,
        AccountRegistrationStatus newStatus,
        string action,
        string? reason,
        bool assignReaderGroup,
        CancellationToken cancellationToken)
    {
        await EnsureAdministratorAsync(actorUserId);
        var user = await RequireUserAsync(targetUserId);
        if (user.RegistrationStatus != expectedStatus)
        {
            throw new InvalidOperationException(
                "Le compte a changé d’état. Rechargez la liste avant de recommencer.");
        }

        if (newStatus == AccountRegistrationStatus.Suspended &&
            await accessService.HasRoleAsync(
                user,
                SystemRoles.Administrator,
                cancellationToken))
        {
            var activeUsers = await userManager.Users
                .Where(candidate => candidate.RegistrationStatus ==
                                    AccountRegistrationStatus.Active)
                .ToArrayAsync(cancellationToken);
            var activeAdministratorCount = 0;
            foreach (var candidate in activeUsers)
            {
                if (await accessService.HasRoleAsync(
                        candidate,
                        SystemRoles.Administrator,
                        cancellationToken))
                {
                    activeAdministratorCount++;
                }
            }

            if (activeAdministratorCount <= 1)
            {
                throw new InvalidOperationException(
                    "Le dernier administrateur actif ne peut pas être suspendu.");
            }
        }

        await using var transaction = await database.Database
            .BeginTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        user.RegistrationStatus = newStatus;
        user.ReviewedAtUtc = now;
        user.ReviewedByUserId = actorUserId;
        user.RejectionReason =
            newStatus == AccountRegistrationStatus.Rejected ? reason : null;

        EnsureSucceeded(
            await userManager.UpdateAsync(user),
            "mettre à jour le compte");

        if (assignReaderGroup)
        {
            var normalizedName = IdentityBootstrapper.NormalizeGroupName(
                SystemGroups.Readers);
            var group = await database.IdentityGroups.SingleAsync(
                candidate => candidate.NormalizedName == normalizedName,
                cancellationToken);
            if (!await database.IdentityGroupMemberships.AnyAsync(
                    membership => membership.GroupId == group.Id &&
                                  membership.UserId == user.Id,
                    cancellationToken))
            {
                database.IdentityGroupMemberships.Add(
                    new IdentityGroupMembershipEntity
                    {
                        GroupId = group.Id,
                        UserId = user.Id,
                        AddedByUserId = actorUserId,
                        AddedAtUtc = now
                    });
            }
        }

        if (newStatus is AccountRegistrationStatus.Suspended or
            AccountRegistrationStatus.Rejected)
        {
            EnsureSucceeded(
                await userManager.UpdateSecurityStampAsync(user),
                "révoquer les sessions du compte");
        }

        database.IdentityAdministrationEvents.Add(
            new IdentityAdministrationEventEntity
            {
                TargetUserId = user.Id,
                ActorUserId = actorUserId,
                Action = action,
                Reason = reason,
                OccurredAtUtc = now
            });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task AddEventAsync(
        Guid targetUserId,
        Guid actorUserId,
        string action,
        string? reason,
        CancellationToken cancellationToken)
    {
        database.IdentityAdministrationEvents.Add(
            new IdentityAdministrationEventEntity
            {
                TargetUserId = targetUserId,
                ActorUserId = actorUserId,
                Action = action,
                Reason = reason,
                OccurredAtUtc = timeProvider.GetUtcNow()
            });
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureAdministratorAsync(Guid actorUserId)
    {
        var actor = await userManager.FindByIdAsync(actorUserId.ToString());
        if (actor is null ||
            actor.RegistrationStatus != AccountRegistrationStatus.Active ||
            !await accessService.HasRoleAsync(actor, SystemRoles.Administrator))
        {
            throw new UnauthorizedAccessException(
                "Cette action est réservée à un administrateur actif.");
        }
    }

    private async Task<ApologiaIdentityUser> RequireUserAsync(Guid userId) =>
        await userManager.FindByIdAsync(userId.ToString())
        ?? throw new InvalidOperationException("Le compte demandé n’existe plus.");

    private async Task<AccountAdministrationView> ToViewAsync(
        ApologiaIdentityUser user) =>
        new(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            user.RegistrationStatus,
            user.RegisteredAtUtc,
            user.EmailVerifiedAtUtc,
            user.ReviewedAtUtc,
            user.RejectionReason,
            user.LockoutEnd,
            await accessService.GetEffectiveRolesAsync(user),
            await accessService.GetGroupsAsync(user.Id));

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
}
