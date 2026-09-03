using ApologiaStudio.Domain.Users;
using ApologiaStudio.Infrastructure.Persistence;
using ApologiaStudio.Infrastructure.Persistence.Identity;
using ApologiaStudio.Web.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ApologiaStudio.IntegrationTests.Persistence;

[Collection(PostgreSqlDatabaseCollection.Name)]
public sealed class PostgreSqlIdentityAdministrationTests
{
    [Fact]
    public async Task IdentityWorkflow_ShouldUseGroupsAndClosedPermissionCatalog()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "APOLOGIASTUDIO_TEST_DB_CONNECTION");
        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "APOLOGIASTUDIO_TEST_DB_CONNECTION was not configured.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<ApologiaStudioDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddIdentityCore<ApologiaIdentityUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = true;
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApologiaStudioDbContext>()
            .AddClaimsPrincipalFactory<ApologiaUserClaimsPrincipalFactory>()
            .AddDefaultTokenProviders();
        services.AddScoped<IdentityAccessService>();
        services.AddScoped<AccountAdministrationService>();
        services.AddScoped<AccessAdministrationService>();
        services.AddSingleton(new IdentityBootstrapOptions(
            true,
            "admin.integration@apologia.local",
            "Integration-Admin-2026!",
            "Integration Administrator"));
        services.AddSingleton<IdentityBootstrapper>();

        await using var provider = services.BuildServiceProvider();
        await using (var migrationScope = provider.CreateAsyncScope())
        {
            var database = migrationScope.ServiceProvider
                .GetRequiredService<ApologiaStudioDbContext>();
            await database.Database.EnsureDeletedAsync();
            await database.Database.MigrateAsync();
        }

        await provider.GetRequiredService<IdentityBootstrapper>()
            .InitializeAsync(CancellationToken.None);

        Guid administratorId;
        Guid readerId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<ApologiaIdentityUser>>();
            var access = scope.ServiceProvider
                .GetRequiredService<IdentityAccessService>();
            var accounts = scope.ServiceProvider
                .GetRequiredService<AccountAdministrationService>();
            var administrator = await users.FindByEmailAsync(
                "admin.integration@apologia.local");
            Assert.NotNull(administrator);
            administratorId = administrator.Id;
            Assert.Empty(await users.GetRolesAsync(administrator));
            Assert.Contains(
                SystemGroups.Administrators,
                await access.GetGroupsAsync(administrator.Id));
            Assert.Equal(
                SystemPermissions.All.Order(),
                (await access.GetEffectivePermissionsAsync(administrator)).Order());
            var principalFactory = scope.ServiceProvider
                .GetRequiredService<IUserClaimsPrincipalFactory<ApologiaIdentityUser>>();
            var administratorPrincipal = await principalFactory.CreateAsync(administrator);
            Assert.True(administratorPrincipal.IsInRole(SystemRoles.Administrator));
            Assert.Contains(
                administratorPrincipal.Claims,
                claim => claim.Type == SystemPermissions.ClaimType &&
                         claim.Value == SystemPermissions.ManageAccounts);

            var reader = new ApologiaIdentityUser
            {
                Id = Guid.NewGuid(),
                UserName = "reader.integration@apologia.local",
                Email = "reader.integration@apologia.local",
                DisplayName = "Integration Reader",
                EmailConfirmed = false,
                RegistrationStatus = AccountRegistrationStatus.PendingEmail,
                RegisteredAtUtc = DateTimeOffset.UtcNow,
                LockoutEnabled = true,
                SecurityStamp = Guid.NewGuid().ToString("N")
            };
            var creation = await users.CreateAsync(
                reader,
                "Integration-Reader-2026!");
            Assert.True(
                creation.Succeeded,
                string.Join("; ", creation.Errors.Select(error => error.Description)));
            readerId = reader.Id;

            var confirmation = new ApprovedAccountConfirmation();
            Assert.False(await confirmation.IsConfirmedAsync(users, reader));
            var confirmationToken = await users.GenerateEmailConfirmationTokenAsync(reader);
            var confirmed = await users.ConfirmEmailAsync(reader, confirmationToken);
            Assert.True(confirmed.Succeeded);
            reader.RegistrationStatus = AccountRegistrationStatus.PendingApproval;
            reader.EmailVerifiedAtUtc = DateTimeOffset.UtcNow;
            Assert.True((await users.UpdateAsync(reader)).Succeeded);
            Assert.False(await confirmation.IsConfirmedAsync(users, reader));

            await accounts.ApproveAsync(
                reader.Id,
                administrator.Id,
                CancellationToken.None);
            reader = await users.FindByIdAsync(reader.Id.ToString());
            Assert.NotNull(reader);
            Assert.Equal(AccountRegistrationStatus.Active, reader.RegistrationStatus);
            Assert.True(await confirmation.IsConfirmedAsync(users, reader));
            Assert.Contains(
                SystemGroups.Readers,
                await access.GetGroupsAsync(reader.Id));
            Assert.Equal(
                [SystemPermissions.AccessStudio],
                await access.GetEffectivePermissionsAsync(reader));

            var directlyActivated = await accounts.CreateAsync(
                administrator.Id,
                "Directly Activated Reader",
                "direct-reader.integration@apologia.local",
                "Integration-Direct-Reader-2026!",
                bypassEmailVerification: true,
                CancellationToken.None);
            Assert.Null(directlyActivated.EmailConfirmationToken);
            Assert.True(directlyActivated.User.EmailConfirmed);
            Assert.NotNull(directlyActivated.User.EmailVerifiedAtUtc);
            Assert.Equal(
                AccountRegistrationStatus.Active,
                directlyActivated.User.RegistrationStatus);
            Assert.Contains(
                SystemGroups.Readers,
                await access.GetGroupsAsync(directlyActivated.User.Id));

            var accessDetails = await accounts.GetAccessDetailsAsync(
                directlyActivated.User.Id,
                administrator.Id,
                CancellationToken.None);
            var readersGroup = Assert.Single(
                accessDetails.Groups,
                group => group.Name == SystemGroups.Readers);
            var editorsGroup = Assert.Single(
                accessDetails.Groups,
                group => group.Name == SystemGroups.Editors);
            Assert.True(readersGroup.IsSelected);
            Assert.False(editorsGroup.IsSelected);
            Assert.Contains(SystemRoles.Editor, editorsGroup.Roles);

            await accounts.UpdateGroupsAsync(
                directlyActivated.User.Id,
                administrator.Id,
                [readersGroup.Id, editorsGroup.Id],
                CancellationToken.None);
            Assert.Equal(
                [SystemGroups.Editors, SystemGroups.Readers],
                await access.GetGroupsAsync(directlyActivated.User.Id));
            Assert.Contains(
                SystemPermissions.ReviewEditorial,
                await access.GetEffectivePermissionsAsync(directlyActivated.User));

            var administratorDetails = await accounts.GetAccessDetailsAsync(
                administrator.Id,
                administrator.Id,
                CancellationToken.None);
            var removeFinalAdministrator = await Assert.ThrowsAsync<InvalidOperationException>(
                () => accounts.UpdateGroupsAsync(
                    administrator.Id,
                    administrator.Id,
                    administratorDetails.Groups
                        .Where(group => group.Name != SystemGroups.Administrators)
                        .Select(group => group.Id)
                        .ToArray(),
                    CancellationToken.None));
            Assert.Contains("dernier administrateur", removeFinalAdministrator.Message);

            var awaitingVerification = await accounts.CreateAsync(
                administrator.Id,
                "Awaiting Email Reader",
                "pending-email.integration@apologia.local",
                "Integration-Pending-Reader-2026!",
                bypassEmailVerification: false,
                CancellationToken.None);
            Assert.NotNull(awaitingVerification.EmailConfirmationToken);
            Assert.False(awaitingVerification.User.EmailConfirmed);
            Assert.Null(awaitingVerification.User.EmailVerifiedAtUtc);
            Assert.Equal(
                AccountRegistrationStatus.PendingEmail,
                awaitingVerification.User.RegistrationStatus);
            Assert.Empty(await access.GetGroupsAsync(awaitingVerification.User.Id));

            var creationEvents = await scope.ServiceProvider
                .GetRequiredService<ApologiaStudioDbContext>()
                .IdentityAdministrationEvents
                .Where(item =>
                    item.TargetUserId == directlyActivated.User.Id &&
                    item.Action == "account.create.verified")
                .ToArrayAsync();
            var creationEvent = Assert.Single(creationEvents);
            Assert.Equal("account.create.verified", creationEvent.Action);
            Assert.Equal("email-verification-bypassed", creationEvent.Reason);
            Assert.Contains(
                await scope.ServiceProvider
                    .GetRequiredService<ApologiaStudioDbContext>()
                    .IdentityAdministrationEvents
                    .Where(item => item.TargetUserId == directlyActivated.User.Id)
                    .ToArrayAsync(),
                item => item.Action == "account.groups.update");
        }

        Guid customGroupId;
        Guid customRoleId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var administration = scope.ServiceProvider
                .GetRequiredService<AccessAdministrationService>();
            customRoleId = await administration.CreateRoleAsync(
                administratorId,
                "Document Reviewer",
                CancellationToken.None);
            await administration.UpdateRolePermissionsAsync(
                administratorId,
                customRoleId,
                [
                    SystemPermissions.AccessStudio,
                    SystemPermissions.ReviewEditorial
                ],
                CancellationToken.None);
            customGroupId = await administration.CreateGroupAsync(
                administratorId,
                "Review Committee",
                "Editorial test group",
                CancellationToken.None);
            await administration.UpdateGroupAsync(
                administratorId,
                customGroupId,
                "Review Committee",
                "Editorial test group",
                [customRoleId],
                [readerId],
                CancellationToken.None);

            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<ApologiaIdentityUser>>();
            var access = scope.ServiceProvider
                .GetRequiredService<IdentityAccessService>();
            var reader = await users.FindByIdAsync(readerId.ToString());
            Assert.NotNull(reader);
            Assert.Contains(
                SystemPermissions.ReviewEditorial,
                await access.GetEffectivePermissionsAsync(reader));

            var invalidPermission = await Assert.ThrowsAsync<InvalidOperationException>(
                () => administration.UpdateRolePermissionsAsync(
                    administratorId,
                    customRoleId,
                    ["permission.invented.at.runtime"],
                    CancellationToken.None));
            Assert.Contains("catalogue", invalidPermission.Message);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var administration = scope.ServiceProvider
                .GetRequiredService<AccessAdministrationService>();
            var snapshot = await administration.GetSnapshotAsync(
                administratorId,
                CancellationToken.None);
            var administrators = Assert.Single(
                snapshot.Groups,
                group => group.Name == SystemGroups.Administrators);
            var protectedRemoval = await Assert.ThrowsAsync<InvalidOperationException>(
                () => administration.UpdateGroupAsync(
                    administratorId,
                    administrators.Id,
                    administrators.Name,
                    administrators.Description,
                    administrators.RoleIds,
                    [],
                    CancellationToken.None));
            Assert.Contains("dernier administrateur", protectedRemoval.Message);

            var administratorRole = Assert.Single(
                snapshot.Roles,
                role => role.Name == SystemRoles.Administrator);
            var backupGroupId = await administration.CreateGroupAsync(
                administratorId,
                "Backup Administrators",
                "Exercises the final-administrator invariant",
                CancellationToken.None);
            await administration.UpdateGroupAsync(
                administratorId,
                backupGroupId,
                "Backup Administrators",
                "Exercises the final-administrator invariant",
                [administratorRole.Id],
                [administratorId],
                CancellationToken.None);
            await administration.UpdateGroupAsync(
                administratorId,
                administrators.Id,
                administrators.Name,
                administrators.Description,
                administrators.RoleIds,
                [],
                CancellationToken.None);
            var protectedRoleRemoval = await Assert.ThrowsAsync<InvalidOperationException>(
                () => administration.UpdateGroupAsync(
                    administratorId,
                    backupGroupId,
                    "Backup Administrators",
                    "Exercises the final-administrator invariant",
                    [],
                    [administratorId],
                    CancellationToken.None));
            Assert.Contains("dernier administrateur", protectedRoleRemoval.Message);
            await administration.UpdateGroupAsync(
                administratorId,
                administrators.Id,
                administrators.Name,
                administrators.Description,
                administrators.RoleIds,
                [administratorId],
                CancellationToken.None);
            await administration.UpdateGroupAsync(
                administratorId,
                backupGroupId,
                "Backup Administrators",
                "Exercises the final-administrator invariant",
                [],
                [],
                CancellationToken.None);
            await administration.DeleteGroupAsync(
                administratorId,
                backupGroupId,
                CancellationToken.None);

            await administration.DeleteGroupAsync(
                administratorId,
                customGroupId,
                CancellationToken.None);
            await administration.DeleteRoleAsync(
                administratorId,
                customRoleId,
                CancellationToken.None);
        }
    }
}
