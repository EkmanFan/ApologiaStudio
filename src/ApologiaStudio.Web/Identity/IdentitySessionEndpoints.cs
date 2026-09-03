using ApologiaStudio.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ApologiaStudio.Web.Identity;

internal static class IdentitySessionEndpoints
{
    public static IEndpointConventionBuilder MapIdentitySessionEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/account");

        group.MapPost(
                "/logout",
                async (
                    [FromServices] SignInManager<ApologiaIdentityUser> signInManager,
                    [FromForm] string? returnUrl) =>
                {
                    await signInManager.SignOutAsync();
                    return TypedResults.LocalRedirect(
                        string.IsNullOrWhiteSpace(returnUrl)
                            ? "~/account/login"
                            : $"~/{returnUrl.TrimStart('/')}");
                })
            .RequireAuthorization();

        return group;
    }
}
