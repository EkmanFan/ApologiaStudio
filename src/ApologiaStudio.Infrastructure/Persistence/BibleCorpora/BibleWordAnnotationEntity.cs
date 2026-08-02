namespace ApologiaStudio.Infrastructure.Persistence.BibleCorpora;

internal sealed class BibleWordAnnotationEntity
{
    public long Id { get; set; }

    public long VerseId { get; set; }

    public int SourceOrdinal { get; set; }

    public string Marker { get; set; } = string.Empty;

    public string AttributeName { get; set; } = string.Empty;

    public string AttributeValue { get; set; } = string.Empty;

    public int CharacterOffset { get; set; }

    public int CharacterLength { get; set; }
}
