using System.Text;
using ApologiaStudio.Application.BibleCorpora.Ingestion;
using ApologiaStudio.Domain.BibleCorpora;
using SIL.Machine.Corpora;

namespace ApologiaStudio.Infrastructure.BibleCorpora.Ingestion;

public sealed class SilMachineUsfmCorpusReader : IBibleCorpusReader
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
    private static readonly IReadOnlySet<string> Extensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".sfm", ".usfm" };

    public async Task<BibleCorpusReadResult> ReadAsync(
        BibleCorpusReadRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sourceDirectory = Path.GetFullPath(request.SourceDirectory);
        if (!Directory.Exists(sourceDirectory))
        {
            throw new BibleCorpusReadException(
                $"USFM source directory does not exist: {sourceDirectory}");
        }

        var files = Directory
            .EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
            .Where(file => Extensions.Contains(Path.GetExtension(file)))
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToArray();

        if (files.Length == 0)
        {
            throw new BibleCorpusReadException(
                $"No USFM files with extensions .sfm or .usfm were found under {sourceDirectory}.");
        }

        var books = new List<ParsedBibleBook>();
        var verses = new List<ParsedBibleVerse>();
        var bookSources = new Dictionary<UsfmBookCode, string>();
        var verseSources = new Dictionary<BibleReference, ParsedBibleVerse>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(sourceDirectory, file)
                .Replace(Path.DirectorySeparatorChar, '/');
            StrictUsfmBibleBookHandler handler;

            try
            {
                var usfm = await File.ReadAllTextAsync(file, StrictUtf8, cancellationToken);
                handler = new StrictUsfmBibleBookHandler(file, relativePath);
                UsfmParser.Parse(usfm, handler, preserveWhitespace: false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (BibleCorpusReadException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new BibleCorpusReadException(
                    $"Unable to parse USFM file {file}: {exception.Message}",
                    exception);
            }

            if (handler.BookCode is not { } bookCode)
            {
                throw new BibleCorpusReadException(
                    $"USFM file contains no valid \\id marker: {file}");
            }

            if (request.ExcludedBookCodes.Contains(bookCode))
            {
                continue;
            }

            if (!ProtestantBibleBookCatalog.TryGetOrdinal(bookCode, out var bookOrdinal))
            {
                throw new BibleCorpusReadException(
                    $"USFM book {bookCode} is outside the configured Protestant 66-book canon. "
                    + "Exclude it explicitly or extend the canon policy.");
            }

            if (handler.Verses.Count == 0)
            {
                throw new BibleCorpusReadException($"USFM file contains no verses: {file}");
            }

            if (bookSources.TryGetValue(bookCode, out var existingBookSource))
            {
                throw new BibleCorpusReadException(
                    $"Duplicate USFM book {bookCode} in {existingBookSource} and {relativePath}.");
            }

            bookSources.Add(bookCode, relativePath);
            books.Add(handler.CreateBook(bookOrdinal));

            foreach (var verse in handler.Verses)
            {
                if (verseSources.TryGetValue(verse.Reference, out var existingVerse))
                {
                    throw new BibleCorpusReadException(
                        $"Duplicate USFM reference {verse.Reference} in "
                        + $"{existingVerse.SourceRelativePath}:{existingVerse.SourceLine} and "
                        + $"{verse.SourceRelativePath}:{verse.SourceLine}.");
                }

                verseSources.Add(verse.Reference, verse);
                verses.Add(verse);
            }
        }

        if (books.Count == 0)
        {
            throw new BibleCorpusReadException(
                "All discovered USFM books were excluded; the corpus contains no importable books.");
        }

        return new BibleCorpusReadResult(
            books.Count,
            books.OrderBy(book => book.BookOrdinal),
            verses
                .OrderBy(verse => ProtestantBibleBookCatalog.TryGetOrdinal(
                    verse.Reference.BookCode,
                    out var ordinal) ? ordinal : int.MaxValue)
                .ThenBy(verse => verse.Reference.ChapterNumber)
                .ThenBy(verse => verse.VerseOrdinal));
    }
}
