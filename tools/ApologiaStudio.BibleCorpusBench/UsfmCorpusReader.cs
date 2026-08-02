using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SIL.Machine.Corpora;

namespace ApologiaStudio.BibleCorpusBench;

public sealed partial class UsfmCorpusReader
{
    private static readonly IReadOnlySet<string> Extensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".sfm", ".usfm" };

    public CorpusReadResult Read(string path)
    {
        var files = CorpusFileDiscovery.Find(path, Extensions, "USFM");
        var verses = new Dictionary<VerseKey, BibleVerse>();

        foreach (var file in files)
        {
            StrictVerseHandler handler;
            try
            {
                var usfm = File.ReadAllText(file, new UTF8Encoding(false, true));
                handler = new StrictVerseHandler(file);
                UsfmParser.Parse(usfm, handler, preserveWhitespace: false);
            }
            catch (BibleCorpusException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new BibleCorpusException($"Unable to parse USFM file {file}: {exception.Message}", exception);
            }

            if (handler.BookCode is null)
            {
                throw new BibleCorpusException($"USFM file contains no valid \\id marker: {file}");
            }

            if (handler.Verses.Count == 0)
            {
                throw new BibleCorpusException($"USFM file contains no verses: {file}");
            }

            foreach (var verse in handler.Verses)
            {
                if (verses.TryGetValue(verse.Key, out var existing))
                {
                    throw new BibleCorpusException(
                        $"Duplicate USFM reference {verse.Key} in {existing.Source} and {verse.Source}.");
                }

                verses.Add(verse.Key, verse);
            }
        }

        var bookCount = verses.Keys.Select(key => key.BookCode).Distinct(StringComparer.Ordinal).Count();
        var strongAttributeCount = verses.Values
            .SelectMany(verse => verse.WordAnnotations)
            .Count(annotation => string.Equals(annotation.Name, "strong", StringComparison.OrdinalIgnoreCase));

        return new CorpusReadResult(verses, files.Count, bookCount, strongAttributeCount);
    }

    private sealed class StrictVerseHandler : UsfmParserHandlerBase
    {
        private readonly string _source;
        private readonly StringBuilder _text = new();
        private readonly List<ParsedWordAnnotation> _annotations = new();
        private VerseKey? _currentKey;
        private int _currentLine;
        private int? _chapter;

        public StrictVerseHandler(string source)
        {
            _source = source;
        }

        public string? BookCode { get; private set; }

        public List<BibleVerse> Verses { get; } = new();

        public override void StartBook(UsfmParserState state, string marker, string code)
        {
            var normalizedCode = code.Trim().ToUpperInvariant();
            if (!BookCodeRegex().IsMatch(normalizedCode))
            {
                Fail(state, $"Invalid USFM book code '{code}'.");
            }

            if (BookCode is not null && !string.Equals(BookCode, normalizedCode, StringComparison.Ordinal))
            {
                Fail(state, $"A second book id '{normalizedCode}' was found after '{BookCode}'.");
            }

            BookCode = normalizedCode;
        }

        public override void Chapter(
            UsfmParserState state,
            string number,
            string marker,
            string altNumber,
            string pubNumber)
        {
            FlushVerse();

            if (!int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var chapter)
                || chapter < 1)
            {
                Fail(state, $"Invalid chapter number '{number}'.");
            }

            _chapter = chapter;
        }

        public override void Verse(
            UsfmParserState state,
            string number,
            string marker,
            string altNumber,
            string pubNumber)
        {
            FlushVerse();

            if (BookCode is null)
            {
                Fail(state, "A verse was found before a valid \\id marker.");
            }

            if (_chapter is null)
            {
                Fail(state, "A verse was found before a valid \\c marker.");
            }

            var normalizedVerse = number.Trim().ToLowerInvariant();
            if (!VerseNumberRegex().IsMatch(normalizedVerse))
            {
                Fail(state, $"Unsupported verse number or range '{number}'.");
            }

            _currentKey = new VerseKey(BookCode, _chapter.Value, normalizedVerse);
            _currentLine = state.LineNumber;
        }

        public override void StartPara(
            UsfmParserState state,
            string marker,
            bool unknown,
            IReadOnlyList<UsfmAttribute> attributes)
        {
            if (unknown)
            {
                Fail(state, $"Unknown paragraph marker \\{marker}.");
            }

            if (_currentKey is not null && state.IsVerseText)
            {
                AppendBoundary();
            }
        }

        public override void StartChar(
            UsfmParserState state,
            string markerWithoutPlus,
            bool unknown,
            IReadOnlyList<UsfmAttribute> attributes)
        {
            if (unknown)
            {
                Fail(state, $"Unknown or invalid character marker \\{markerWithoutPlus}.");
            }

            if (_currentKey is null || !state.IsVerseText || attributes is null)
            {
                return;
            }

            foreach (var attribute in attributes)
            {
                _annotations.Add(new ParsedWordAnnotation(
                    markerWithoutPlus,
                    attribute.Name,
                    attribute.Value,
                    GetNextVisibleCharacterOffset()));
            }
        }

        public override void EndChar(
            UsfmParserState state,
            string marker,
            IReadOnlyList<UsfmAttribute> attributes,
            bool closed)
        {
            // Note submarkers such as \fr, \ft, \xo, and \xt are normally
            // closed implicitly by the next note submarker or by the note end.
            if (!closed && state.NoteTag is null)
            {
                Fail(state, $"Character marker \\{marker} was not explicitly closed.");
            }
        }

        public override void EndNote(UsfmParserState state, string marker, bool closed)
        {
            if (!closed)
            {
                Fail(state, $"Note marker \\{marker} was not explicitly closed.");
            }
        }

        public override void EndSidebar(UsfmParserState state, string marker, bool closed)
        {
            if (!closed)
            {
                Fail(state, $"Sidebar marker \\{marker} was not explicitly closed.");
            }
        }

        public override void Unmatched(UsfmParserState state, string marker)
        {
            Fail(state, $"Unmatched USFM marker \\{marker}.");
        }

        public override void Text(UsfmParserState state, string text)
        {
            if (_currentKey is not null && state.IsVerseText && !state.IsSpecialText)
            {
                _text.Append(text);
            }
        }

        public override void StartCell(UsfmParserState state, string marker, string align, int colspan)
        {
            if (_currentKey is not null && state.IsVerseText)
            {
                AppendBoundary();
            }
        }

        public override void OptBreak(UsfmParserState state)
        {
            if (_currentKey is not null && state.IsVerseText)
            {
                AppendBoundary();
            }
        }

        public override void EndUsfm(UsfmParserState state)
        {
            FlushVerse();
        }

        private void AppendBoundary()
        {
            if (_text.Length > 0 && !char.IsWhiteSpace(_text[^1]))
            {
                _text.Append(' ');
            }
        }

        private int GetNextVisibleCharacterOffset()
        {
            // Add a sentinel so a trailing whitespace run is represented by the
            // single space that will precede the annotated word after normalization.
            return TextNormalizer.Normalize(_text.ToString() + "x").Length - 1;
        }

        private void FlushVerse()
        {
            if (_currentKey is null)
            {
                return;
            }

            Verses.Add(new BibleVerse(
                _currentKey.Value,
                TextNormalizer.Normalize(_text.ToString()),
                _source,
                _currentLine,
                _annotations.ToArray()));

            _currentKey = null;
            _currentLine = 0;
            _text.Clear();
            _annotations.Clear();
        }

        [DoesNotReturn]
        private void Fail(UsfmParserState state, string message)
        {
            throw new BibleCorpusException(
                $"{_source}:{state.LineNumber}:{state.ColumnNumber}: {message}");
        }
    }

    [GeneratedRegex("^[1-3]?[A-Z]{2,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex BookCodeRegex();

    [GeneratedRegex("^[0-9]+[a-z]?(?:[-,][0-9]+[a-z]?)*$", RegexOptions.CultureInvariant)]
    private static partial Regex VerseNumberRegex();
}
