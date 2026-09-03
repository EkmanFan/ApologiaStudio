using System.Security.Claims;
using ApologiaStudio.Domain.Users;
using ApologiaStudio.Infrastructure.Persistence;
using ApologiaStudio.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Web.Identity;

public sealed class IdentityBootstrapper(
    IServiceScopeFactory scopeFactory,
    IdentityBootstrapOptions options,
    TimeProvider timeProvider,
    ILogger<IdentityBootstrapper> logger)
{
    private static readonly Guid HistoricalDemoUserId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider
            .GetRequiredService<ApologiaStudioDbContext>();
        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var roleDefinition in RoleDefinitions)
        {
            var role = await roleManager.FindByNameAsync(roleDefinition.Name);
            if (role is not null)
            {
                continue;
            }

            role = new IdentityRole<Guid>(roleDefinition.Name)
            {
                Id = Guid.NewGuid()
            };
            EnsureSucceeded(
                await roleManager.CreateAsync(role),
                $"create role {roleDefinition.Name}");

            foreach (var permission in roleDefinition.Permissions)
            {
                EnsureSucceeded(
                    await roleManager.AddClaimAsync(
                        role,
                        new Claim(SystemPermissions.ClaimType, permission)),
                    $"add permission {permission} to {roleDefinition.Name}");
            }
        }

        foreach (var groupDefinition in SystemGroupDefinitions)
        {
            var normalizedName = NormalizeGroupName(groupDefinition.Name);
            var group = await database.IdentityGroups.SingleOrDefaultAsync(
                candidate => candidate.NormalizedName == normalizedName,
                cancellationToken);
            if (group is null)
            {
                group = new IdentityGroupEntity
                {
                    Id = groupDefinition.Id,
                    Name = groupDefinition.Name,
                    NormalizedName = normalizedName,
                    Description = groupDefinition.Description,
                    IsSystem = true,
                    CreatedAtUtc = timeProvider.GetUtcNow()
                };
                database.IdentityGroups.Add(group);
                await database.SaveChangesAsync(cancellationToken);
            }

            var role = await roleManager.FindByNameAsync(groupDefinition.Role)
                ?? throw new InvalidOperationException(
                    $"The required role {groupDefinition.Role} was not created.");
            if (!await database.IdentityGroupRoles.AnyAsync(
                    assignment => assignment.GroupId == group.Id &&
                                  assignment.RoleId == role.Id,
                    cancellationToken))
            {
                database.IdentityGroupRoles.Add(
                    new IdentityGroupRoleEntity
                    {
                        GroupId = group.Id,
                        RoleId = role.Id,
                        AssignedAtUtc = timeProvider.GetUtcNow()
                    });
                await database.SaveChangesAsync(cancellationToken);
            }
        }

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApologiaIdentityUser>>();
        if (options.Enabled &&
            !await userManager.Users.AnyAsync(cancellationToken))
        {
            await CreateBootstrapAdministratorAsync(
                userManager,
                database,
                cancellationToken);
        }

        await MigrateDirectSystemRolesToGroupsAsync(
            database,
            roleManager,
            cancellationToken);
    }

    private async Task CreateBootstrapAdministratorAsync(
        UserManager<ApologiaIdentityUser> userManager,
        ApologiaStudioDbContext database,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var administrator = new ApologiaIdentityUser
        {
            Id = HistoricalDemoUserId,
            UserName = options.Email,
            Email = options.Email,
            NormalizedUserName = userManager.NormalizeName(options.Email!),
            NormalizedEmail = userManager.NormalizeEmail(options.Email!),
            DisplayName = options.DisplayName,
            EmailConfirmed = true,
            EmailVerifiedAtUtc = now,
            RegistrationStatus = AccountRegistrationStatus.Active,
            RegisteredAtUtc = now,
            ReviewedAtUtc = now,
            ReviewedByUserId = HistoricalDemoUserId,
            LockoutEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };

        EnsureSucceeded(
            await userManager.CreateAsync(administrator, options.Password!),
            "create bootstrap administrator");
        EnsureSucceeded(
            await userManager.AddToRoleAsync(
                administrator,
                SystemRoles.Administrator),
            "assign bootstrap administrator role");

        database.IdentityAdministrationEvents.Add(
            new IdentityAdministrationEventEntity
            {
                TargetUserId = administrator.Id,
                ActorUserId = administrator.Id,
                Action = "bootstrap",
                OccurredAtUtc = now
            });
        await database.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Created the first local administrator account {Email}. Disable bootstrap configuration after first use.",
            administrator.Email);
    }

    private async Task MigrateDirectSystemRolesToGroupsAsync(
        ApologiaStudioDbContext database,
        RoleManager<IdentityRole<Guid>> roleManager,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var groupDefinition in SystemGroupDefinitions)
        {
            var group = await database.IdentityGroups.SingleAsync(
                candidate => candidate.NormalizedName ==
                             NormalizeGroupName(groupDefinition.Name),
                cancellationToken);
            var role = await roleManager.FindByNameAsync(groupDefinition.Role)
                ?? throw new InvalidOperationException(
                    $"The required role {groupDefinition.Role} is missing.");
            var userIds = await database.UserRoles
                .Where(userRole => userRole.RoleId == role.Id)
                .Select(userRole => userRole.UserId)
                .ToArrayAsync(cancellationToken);

            foreach (var userId in userIds)
            {
                if (await database.IdentityGroupMemberships.AnyAsync(
                        membership => membership.GroupId == group.Id &&
                                      membership.UserId == userId,
                        cancellationToken))
                {
                    continue;
                }

                database.IdentityGroupMemberships.Add(
                    new IdentityGroupMembershipEntity
                    {
                        GroupId = group.Id,
                        UserId = userId,
                        AddedByUserId = userId,
                        AddedAtUtc = now
                    });
            }

            var legacyAssignments = await database.UserRoles
                .Where(userRole => userRole.RoleId == role.Id)
                .ToArrayAsync(cancellationToken);
            database.UserRoles.RemoveRange(legacyAssignments);
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    public static string NormalizeGroupName(string name) =>
        name.Trim().ToUpperInvariant();

    private static void EnsureSucceeded(
        IdentityResult result,
        string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Unable to {operation}: " +
            string.Join("; ", result.Errors.Select(error => error.Description)));
    }

    private static IReadOnlyList<RoleDefinition> RoleDefinitions { get; } =
    [
        new(SystemRoles.Reader, [SystemPermissions.AccessStudio]),
        new(SystemRoles.Editor,
        [
            SystemPermissions.AccessStudio,
            SystemPermissions.ReviewEditorial
        ]),
        new(SystemRoles.DocumentOperator,
        [
            SystemPermissions.AccessStudio,
            SystemPermissions.OperateDocumentManager,
            SystemPermissions.ReplayDocumentDelivery
        ]),
        new(SystemRoles.Administrator, SystemPermissions.All)
    ];

    private sealed record RoleDefinition(
        string Name,
        IReadOnlyList<string> Permissions);

    private static IReadOnlyList<SystemGroupDefinition>
        SystemGroupDefinitions { get; } =
    [
        new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            SystemGroups.Readers,
            SystemRoles.Reader,
            "Accès de base à Apologia Studio."),
        new(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            SystemGroups.Editors,
            SystemRoles.Editor,
            "Revue et validation éditoriales."),
        new(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            SystemGroups.DocumentOperators,
            SystemRoles.DocumentOperator,
            "Exploitation du Document Manager."),
        new(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            SystemGroups.Administrators,
            SystemRoles.Administrator,
            "Administration complète d’Apologia Studio.")
    ];

    private sealed record SystemGroupDefinition(
        Guid Id,
        string Name,
        string Role,
        string Description);
}
