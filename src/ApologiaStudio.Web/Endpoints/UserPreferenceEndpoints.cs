using ApologiaStudio.Application.Preferences;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.Web.Endpoints;

public static class UserPreferenceEndpoints
{
    public static IEndpointRouteBuilder MapUserPreferenceEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/preferences/theme",
                GetThemeAsync)
            .RequireAuthorization(SystemPermissions.AccessStudio);

        return endpoints;
    }

    private static async Task<IResult> GetThemeAsync(
        GetUserPreferencesHandler handler,
        CancellationToken cancellationToken)
    {
        var preferences = await handler.HandleAsync(cancellationToken);

        return Results.Ok(
            new ThemePreferenceResponse(
                preferences.ThemeMode.ToString().ToLowerInvariant(),
                preferences.ThemeColor,
                preferences.DarkPageColor,
                preferences.DarkSurfaceColor));
    }

    private sealed record ThemePreferenceResponse(
        string Mode,
        string Color,
        string DarkPageColor,
        string DarkSurfaceColor);
}
