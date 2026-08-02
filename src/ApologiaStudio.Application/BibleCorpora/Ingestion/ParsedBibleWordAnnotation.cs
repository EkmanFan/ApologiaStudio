namespace ApologiaStudio.Application.BibleCorpora.Ingestion;

public sealed record ParsedBibleWordAnnotation
{
    public ParsedBibleWordAnnotation(
        int sourceOrdinal,
        string marker,
        string name,
        string value,
        int characterOffset,
        int characterLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sourceOrdinal, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegative(characterOffset);
        ArgumentOutOfRangeException.ThrowIfLessThan(characterLength, 1);

        SourceOrdinal = sourceOrdinal;
        Marker = marker.Trim();
        Name = name.Trim();
        Value = value;
        CharacterOffset = characterOffset;
        CharacterLength = characterLength;
    }

    public int SourceOrdinal { get; }

    public string Marker { get; }

    public string Name { get; }

    public string Value { get; }

    public int CharacterOffset { get; }

    public int CharacterLength { get; }
}
