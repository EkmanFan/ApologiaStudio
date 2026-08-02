using ApologiaStudio.Domain.BibleCorpora;

namespace ApologiaStudio.Application.BibleCorpora.Ingestion;

public sealed record ParsedBibleSupplementalText
{
    public ParsedBibleSupplementalText(
        int sourceOrdinal,
        string marker,
        string text,
        BibleSupplementalTextPlacement placement,
        int? characterOffset)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sourceOrdinal, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (!Enum.IsDefined(placement))
        {
            throw new ArgumentOutOfRangeException(nameof(placement));
        }

        if (placement == BibleSupplementalTextPlacement.Within)
        {
            if (characterOffset is null or < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(characterOffset),
                    "Supplemental text within a verse requires a non-negative character offset.");
            }
        }
        else if (characterOffset is not null)
        {
            throw new ArgumentException(
                "Only supplemental text within a verse can have a character offset.",
                nameof(characterOffset));
        }

        SourceOrdinal = sourceOrdinal;
        Marker = marker.Trim();
        Text = text.Trim();
        Placement = placement;
        CharacterOffset = characterOffset;
    }

    public int SourceOrdinal { get; }

    public string Marker { get; }

    public string Text { get; }

    public BibleSupplementalTextPlacement Placement { get; }

    public int? CharacterOffset { get; }
}
