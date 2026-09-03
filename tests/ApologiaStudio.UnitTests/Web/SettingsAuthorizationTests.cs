using ApologiaStudio.Domain.Users;
using ApologiaStudio.Web.Components.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace ApologiaStudio.UnitTests.Web;

public sealed class SettingsAuthorizationTests
{
    [Fact]
    public void PersonalPreferences_RequireStudioAccess()
    {
        Assert.Contains(
            typeof(Settings).GetCustomAttributes(
                typeof(RouteAttribute),
                inherit: true).Cast<RouteAttribute>(),
            route => route.Template == "/settings");
        Assert.Contains(
            typeof(Settings).GetCustomAttributes(
                typeof(AuthorizeAttribute),
                inherit: true).Cast<AuthorizeAttribute>(),
            authorization => authorization.Policy == SystemPermissions.AccessStudio);
    }

    [Fact]
    public void AiAndAgentAdministration_RequireGlobalSettingsPermission()
    {
        var aiRoutes = typeof(AdministrationSettings)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Select(route => route.Template)
            .ToArray();
        var agentRoutes = typeof(AdministrationAgents)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Select(route => route.Template)
            .ToArray();

        Assert.Contains("/administration/ai", aiRoutes);
        Assert.Contains("/administration/agents", agentRoutes);
        Assert.Contains(
            typeof(AdministrationSettings).GetCustomAttributes(
                typeof(AuthorizeAttribute),
                inherit: true).Cast<AuthorizeAttribute>(),
            authorization => authorization.Policy == SystemPermissions.ManageSettings);
        Assert.Contains(
            typeof(AdministrationAgents).GetCustomAttributes(
                typeof(AuthorizeAttribute),
                inherit: true).Cast<AuthorizeAttribute>(),
            authorization => authorization.Policy == SystemPermissions.ManageSettings);
    }
}
