using ApologiaStudio.Application.Abstractions.BibleCorpora;
using ApologiaStudio.Domain.BibleCorpora;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.Web.Endpoints;

public static class BibleCorpusEndpoints
{
    public static IEndpointRouteBuilder MapBibleCorpusEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/bible")
            .WithTags("Bible")
            .RequireAuthorization(SystemPermissions.AccessStudio);

        group.MapGet(
            "/editions",
            ListEditionsAsync);

        group.MapGet(
            "/editions/{editionCode}/books",
            ListBooksAsync);

        group.MapGet(
            "/editions/{editionCode}/books/{bookCode}/chapters/{chapterNumber:int}",
            GetChapterAsync);

        group.MapGet(
            "/editions/{editionCode}/books/{bookCode}/chapters/{chapterNumber:int}/verses/{verseLabel}",
            GetVerseAsync);

        return endpoints;
    }

    private static async Task<IResult> ListEditionsAsync(
        IBibleCorpusQueryRepository repository,
        CancellationToken cancellationToken)
    {
        var editions = await repository.ListActiveEditionsAsync(
            cancellationToken);

        return Results.Ok(editions);
    }

    private static async Task<IResult> ListBooksAsync(
        string editionCode,
        IBibleCorpusQueryRepository repository,
        CancellationToken cancellationToken)
    {
        if (!TryCreateEditionCode(
                editionCode,
                out var parsedEditionCode))
        {
            return InvalidReference("The Bible edition code is invalid.");
        }

        var result = await repository.GetBooksAsync(
            parsedEditionCode,
            cancellationToken);

        return result is null
            ? Results.NotFound()
            : Results.Ok(result);
    }

    private static async Task<IResult> GetChapterAsync(
        string editionCode,
        string bookCode,
        int chapterNumber,
        IBibleCorpusQueryRepository repository,
        CancellationToken cancellationToken)
    {
        if (!TryCreateCodes(
                editionCode,
                bookCode,
                out var parsedEditionCode,
                out var parsedBookCode)
            || chapterNumber < 1)
        {
            return InvalidReference("The Bible edition, book, or chapter is invalid.");
        }

        var result = await repository.GetChapterAsync(
            parsedEditionCode,
            parsedBookCode,
            chapterNumber,
            cancellationToken);

        return result is null
            ? Results.NotFound()
            : Results.Ok(result);
    }

    private static async Task<IResult> GetVerseAsync(
        string editionCode,
        string bookCode,
        int chapterNumber,
        string verseLabel,
        IBibleCorpusQueryRepository repository,
        CancellationToken cancellationToken)
    {
        if (!TryCreateCodes(
                editionCode,
                bookCode,
                out var parsedEditionCode,
                out var parsedBookCode)
            || chapterNumber < 1)
        {
            return InvalidReference("The Bible edition, book, or chapter is invalid.");
        }

        BibleReference reference;

        try
        {
            reference = new BibleReference(
                parsedBookCode,
                chapterNumber,
                verseLabel);
        }
        catch (ArgumentException)
        {
            return InvalidReference("The verse label is invalid.");
        }

        var result = await repository.GetVerseAsync(
            parsedEditionCode,
            reference,
            cancellationToken);

        return result is null
            ? Results.NotFound()
            : Results.Ok(result);
    }

    private static IResult InvalidReference(
        string detail) =>
        Results.Problem(
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest);

    private static bool TryCreateCodes(
        string editionCode,
        string bookCode,
        out BibleEditionCode parsedEditionCode,
        out UsfmBookCode parsedBookCode)
    {
        parsedBookCode = default;

        if (!TryCreateEditionCode(
                editionCode,
                out parsedEditionCode))
        {
            return false;
        }

        try
        {
            parsedBookCode = new UsfmBookCode(bookCode);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryCreateEditionCode(
        string editionCode,
        out BibleEditionCode parsedEditionCode)
    {
        try
        {
            parsedEditionCode = new BibleEditionCode(editionCode);
            return true;
        }
        catch (ArgumentException)
        {
            parsedEditionCode = default;
            return false;
        }
    }
}
