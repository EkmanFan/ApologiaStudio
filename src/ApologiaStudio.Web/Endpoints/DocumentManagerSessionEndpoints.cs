using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using ApologiaStudio.Domain.Users;
using ApologiaStudio.Web.DocumentManager;
using ApologiaStudio.Web.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace ApologiaStudio.Web.Endpoints;

public static class DocumentManagerSessionEndpoints
{
    public static IEndpointRouteBuilder MapDocumentManagerSessionEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/document-manager/connect",
                CreateExchangeFormAsync)
            .RequireAuthorization(SystemPermissions.OperateDocumentManager);
        return endpoints;
    }

    private static async Task<IResult> CreateExchangeFormAsync(
        HttpContext context,
        DocumentManagerSessionBridgeOptions options,
        DocumentManagerSessionTicketIssuer issuer,
        InterfaceLanguageResolver languageResolver,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(subject, out var userId))
        {
            return Results.Forbid();
        }

        var language = await languageResolver.ResolveAsync(
            userId,
            cancellationToken);
        var ticket = issuer.Issue(context.User, language);
        var safeReturnUrl = NormalizeReturnUrl(returnUrl);
        var scriptNonce = WebEncoders.Base64UrlEncode(
            RandomNumberGenerator.GetBytes(18));
        var exchangeAddress = WebUtility.HtmlEncode(
            options.ExchangeAddress.AbsoluteUri);
        var encodedTicket = WebUtility.HtmlEncode(ticket);
        var encodedReturnUrl = WebUtility.HtmlEncode(safeReturnUrl);
        var html = $$"""
            <!doctype html>
            <html lang="{{(language == ApplicationLanguage.English ? "en" : "fr")}}">
            <head><meta charset="utf-8"><title>Document Manager</title></head>
            <body>
              <form id="manager-session" method="post" action="{{exchangeAddress}}">
                <input type="hidden" name="ticket" value="{{encodedTicket}}">
                <input type="hidden" name="returnUrl" value="{{encodedReturnUrl}}">
                <noscript><button type="submit">Continue to Document Manager</button></noscript>
              </form>
              <script nonce="{{scriptNonce}}">document.getElementById('manager-session').submit();</script>
            </body>
            </html>
            """;

        context.Response.Headers.CacheControl = "no-store, max-age=0";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers.ContentSecurityPolicy =
            $"default-src 'none'; base-uri 'none'; form-action {options.ExchangeAddress.GetLeftPart(UriPartial.Authority)}; script-src 'nonce-{scriptNonce}'; frame-ancestors 'self'";
        return Results.Content(html, "text/html", statusCode: StatusCodes.Status200OK);
    }

    private static string NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) ||
            !returnUrl.StartsWith("/", StringComparison.Ordinal) ||
            returnUrl.StartsWith("//", StringComparison.Ordinal) ||
            Uri.TryCreate(returnUrl, UriKind.Absolute, out _))
        {
            return "/";
        }

        return returnUrl;
    }
}
