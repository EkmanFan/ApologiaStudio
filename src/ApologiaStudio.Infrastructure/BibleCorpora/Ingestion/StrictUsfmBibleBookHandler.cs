using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using ApologiaStudio.Application.BibleCorpora.Ingestion;
using ApologiaStudio.Domain.BibleCorpora;
using SIL.Machine.Corpora;

namespace ApologiaStudio.Infrastructure.BibleCorpora.Ingestion;

internal sealed class StrictUsfmBibleBookHandler : UsfmParserHandlerBase
{
    private static readonly IReadOnlySet<string> MetadataMarkers =
        new HashSet<string>(StringComparer.Ordinal) { "toc1", "toc2", "toc3", "h", "mt1" };

    private readonly string _source;
    private readonly string _sourceRelativePath;
    private readonly StringBuilder _text = new();
    private readonly StringBuilder _supplementalText = new();
    private readonly StringBuilder _metadataText = new();
    private readonly Dictionary<string, string> _metadata = new(StringComparer.Ordinal);
    private readonly Stack<OpenCharacterFrame> _openCharacters = new();
    private readonly List<ParsedBibleWordAnnotation> _annotations = new();
    private readonly List<PendingSupplementalText> _supplementalTexts = new();
    private BibleReference? _currentReference;
    private int _currentLine;
    private int? _chapter;
    private int _verseOrdinal;
    private int _nextAnnotationOrdinal = 1;
    private int _nextSupplementalOrdinal = 1;
    private string? _supplementalMarker;
    private int _supplementalOffset;
    private bool _supplementalBeganWithinVerse;
    private string? _metadataMarker;

    public StrictUsfmBibleBookHandler(string source, string sourceRelativePath)
    {
        _source = source;
        _sourceRelativePath = sourceRelativePath;
    }

    public UsfmBookCode? BookCode { get; private set; }

    public List<ParsedBibleVerse> Verses { get; } = new();

    public ParsedBibleBook CreateBook(int bookOrdinal)
    {
        if (BookCode is not { } bookCode)
        {
            throw new InvalidOperationException("The USFM book has no parsed book code.");
        }

        var displayName = FirstMetadataValue("toc1", "mt1", "h", "toc2", "toc3")
            ?? bookCode.Value;
        var shortName = FirstMetadataValue("toc2", "toc3", "h");
        if (string.Equals(displayName, shortName, StringComparison.Ordinal))
        {
            shortName = null;
        }

        return new ParsedBibleBook(
            bookCode,
            bookOrdinal,
            displayName,
            shortName,
            _sourceRelativePath);
    }

    public override void StartBook(UsfmParserState state, string marker, string code)
    {
        UsfmBookCode normalizedCode;
        try
        {
            normalizedCode = new UsfmBookCode(code);
        }
        catch (ArgumentException)
        {
            Fail(state, $"Invalid USFM book code '{code}'.");
            return;
        }

        if (BookCode is not null)
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

        if (_supplementalTexts.Count > 0)
        {
            Fail(state, "Supplemental text was not anchored to a verse before the next chapter.");
        }

        if (!int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var chapter)
            || chapter < 1)
        {
            Fail(state, $"Invalid chapter number '{number}'.");
        }

        _chapter = chapter;
        _verseOrdinal = 0;
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

        var bookCode = BookCode.Value;

        if (_chapter is null)
        {
            Fail(state, "A verse was found before a valid \\c marker.");
        }

        try
        {
            _currentReference = new BibleReference(bookCode, _chapter.Value, number);
        }
        catch (ArgumentException)
        {
            Fail(state, $"Unsupported verse number or range '{number}'.");
        }

        _verseOrdinal++;
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

        if (string.Equals(marker, "d", StringComparison.Ordinal)
            || string.Equals(marker, "sp", StringComparison.Ordinal))
        {
            if (_supplementalMarker is not null)
            {
                Fail(state, $"Supplemental marker \\{marker} started inside \\{_supplementalMarker}.");
            }

            _supplementalMarker = marker;
            _supplementalBeganWithinVerse = _currentReference is not null;
            _supplementalOffset = GetCurrentNormalizedLength();
            _supplementalText.Clear();
            return;
        }

        if (_chapter is null
            && _currentReference is null
            && MetadataMarkers.Contains(marker))
        {
            _metadataMarker = marker;
            _metadataText.Clear();
            return;
        }

        if (_currentReference is not null && state.IsVerseText)
        {
            AppendBoundary();
        }
    }

    public override void EndPara(UsfmParserState state, string marker)
    {
        if (string.Equals(marker, _supplementalMarker, StringComparison.Ordinal))
        {
            var text = BibleCorpusTextNormalizer.Normalize(_supplementalText.ToString());
            if (text.Length > 0)
            {
                _supplementalTexts.Add(new PendingSupplementalText(
                    _nextSupplementalOrdinal++,
                    marker,
                    text,
                    _supplementalOffset,
                    _supplementalBeganWithinVerse));
            }

            _supplementalMarker = null;
            _supplementalOffset = 0;
            _supplementalBeganWithinVerse = false;
            _supplementalText.Clear();
        }

        if (string.Equals(marker, _metadataMarker, StringComparison.Ordinal))
        {
            var text = BibleCorpusTextNormalizer.Normalize(_metadataText.ToString());
            if (text.Length > 0)
            {
                _metadata.TryAdd(marker, text);
            }

            _metadataMarker = null;
            _metadataText.Clear();
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

        if (string.Equals(markerWithoutPlus, "qs", StringComparison.Ordinal)
            && _currentReference is not null
            && state.IsVerseText)
        {
            AppendBoundary();
        }

        if (_currentReference is null
            || !state.IsVerseText
            || state.NoteTag is not null
            || _supplementalMarker is not null)
        {
            return;
        }

        var startOffset = GetNextVisibleCharacterOffset();
        var parsedAttributes = (attributes ?? Array.Empty<UsfmAttribute>())
            .Select(attribute => new OpenAttribute(
                _nextAnnotationOrdinal++,
                attribute.Name,
                attribute.Value))
            .ToArray();
        _openCharacters.Push(new OpenCharacterFrame(
            markerWithoutPlus,
            startOffset,
            parsedAttributes));
    }

    public override void EndChar(
        UsfmParserState state,
        string marker,
        IReadOnlyList<UsfmAttribute> attributes,
        bool closed)
    {
        if (!closed && state.NoteTag is null)
        {
            Fail(state, $"Character marker \\{marker} was not explicitly closed.");
        }

        if (_openCharacters.Count == 0
            || !string.Equals(_openCharacters.Peek().Marker, marker, StringComparison.Ordinal))
        {
            return;
        }

        var frame = _openCharacters.Pop();
        var length = GetCurrentNormalizedLength() - frame.CharacterOffset;
        if (frame.Attributes.Count > 0 && length < 1)
        {
            Fail(state, $"Attributed character marker \\{marker} contains no visible text.");
        }

        foreach (var attribute in frame.Attributes)
        {
            _annotations.Add(new ParsedBibleWordAnnotation(
                attribute.SourceOrdinal,
                marker,
                attribute.Name,
                attribute.Value,
                frame.CharacterOffset,
                length));
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
        if (_supplementalMarker is not null && state.NoteTag is null && !state.IsSpecialText)
        {
            _supplementalText.Append(text);
            return;
        }

        if (_metadataMarker is not null && state.NoteTag is null && !state.IsSpecialText)
        {
            _metadataText.Append(text);
            return;
        }

        if (_currentReference is not null && state.IsVerseText && !state.IsSpecialText)
        {
            _text.Append(text);
        }
    }

    public override void StartCell(UsfmParserState state, string marker, string align, int colspan)
    {
        if (_currentReference is not null && state.IsVerseText)
        {
            AppendBoundary();
        }
    }

    public override void OptBreak(UsfmParserState state)
    {
        if (_currentReference is not null && state.IsVerseText)
        {
            AppendBoundary();
        }
    }

    public override void EndUsfm(UsfmParserState state)
    {
        FlushVerse();

        if (_supplementalTexts.Count > 0)
        {
            Fail(state, "Supplemental text was not anchored to a verse before the end of the file.");
        }
    }

    private void FlushVerse()
    {
        if (_currentReference is null)
        {
            return;
        }

        if (_openCharacters.Count > 0)
        {
            throw new BibleCorpusReadException(
                $"{_source}:{_currentLine}: A character marker remained open at the end of the verse.");
        }

        var text = BibleCorpusTextNormalizer.Normalize(_text.ToString());
        var supplementalTexts = _supplementalTexts
            .Select(item => item.ToParsed(text.Length))
            .ToArray();

        Verses.Add(new ParsedBibleVerse(
            _currentReference.Value,
            _verseOrdinal,
            text,
            _sourceRelativePath,
            _currentLine,
            _annotations.OrderBy(annotation => annotation.SourceOrdinal),
            supplementalTexts));

        _currentReference = null;
        _currentLine = 0;
        _text.Clear();
        _annotations.Clear();
        _supplementalTexts.Clear();
        _nextAnnotationOrdinal = 1;
        _nextSupplementalOrdinal = 1;
    }

    private void AppendBoundary()
    {
        if (_text.Length > 0 && !char.IsWhiteSpace(_text[^1]))
        {
            _text.Append(' ');
        }
    }

    private int GetCurrentNormalizedLength() =>
        BibleCorpusTextNormalizer.Normalize(_text.ToString()).Length;

    private int GetNextVisibleCharacterOffset() =>
        BibleCorpusTextNormalizer.Normalize(_text.ToString() + "x").Length - 1;

    private string? FirstMetadataValue(params string[] markers)
    {
        foreach (var marker in markers)
        {
            if (_metadata.TryGetValue(marker, out var value))
            {
                return value;
            }
        }

        return null;
    }

    [DoesNotReturn]
    private void Fail(UsfmParserState state, string message)
    {
        throw new BibleCorpusReadException(
            $"{_source}:{state.LineNumber}:{state.ColumnNumber}: {message}");
    }

    private sealed record OpenCharacterFrame(
        string Marker,
        int CharacterOffset,
        IReadOnlyList<OpenAttribute> Attributes);

    private sealed record OpenAttribute(int SourceOrdinal, string Name, string Value);

    private sealed record PendingSupplementalText(
        int SourceOrdinal,
        string Marker,
        string Text,
        int CharacterOffset,
        bool BeganWithinVerse)
    {
        public ParsedBibleSupplementalText ToParsed(int verseTextLength)
        {
            if (!BeganWithinVerse)
            {
                return new ParsedBibleSupplementalText(
                    SourceOrdinal,
                    Marker,
                    Text,
                    BibleSupplementalTextPlacement.Before,
                    null);
            }

            if (CharacterOffset >= verseTextLength)
            {
                return new ParsedBibleSupplementalText(
                    SourceOrdinal,
                    Marker,
                    Text,
                    BibleSupplementalTextPlacement.After,
                    null);
            }

            return new ParsedBibleSupplementalText(
                SourceOrdinal,
                Marker,
                Text,
                BibleSupplementalTextPlacement.Within,
                CharacterOffset);
        }
    }
}
