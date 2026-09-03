using ApologiaStudio.Domain.Users;
using ApologiaStudio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Web.Identity;

public sealed class InterfaceLanguageResolver(
    ApologiaStudioDbContext database)
{
    public async Task<ApplicationLanguage> ResolveAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var typedUserId = new UserId(userId);
        var language = await database.UserPreferences
            .AsNoTracking()
            .Where(preferences => preferences.UserId == typedUserId)
            .Select(preferences => (ApplicationLanguage?)preferences.InterfaceLanguage)
            .SingleOrDefaultAsync(cancellationToken);

        return language ?? UserPreferences.DefaultInterfaceLanguage;
    }
}

public static class InterfaceLanguageCookie
{
    public const string Name = "Apologia.InterfaceLanguage";

    public static ApplicationLanguage Read(HttpContext? context) =>
        context?.Request.Cookies.TryGetValue(Name, out var value) is true &&
        ApplicationLanguageExtensions.TryParseLanguageTag(value, out var language)
            ? language
            : UserPreferences.DefaultInterfaceLanguage;

    public static void Write(
        HttpContext context,
        ApplicationLanguage language)
    {
        context.Response.Cookies.Append(
            Name,
            language.ToLanguageTag(),
            new CookieOptions
            {
                HttpOnly = false,
                IsEssential = true,
                MaxAge = TimeSpan.FromDays(365),
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps
            });
    }
}
