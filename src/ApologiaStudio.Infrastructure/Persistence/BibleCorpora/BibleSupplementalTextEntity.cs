using ApologiaStudio.Domain.BibleCorpora;

namespace ApologiaStudio.Infrastructure.Persistence.BibleCorpora;

internal sealed class BibleSupplementalTextEntity
{
    public long Id { get; set; }

    public long VerseId { get; set; }

    public int SourceOrdinal { get; set; }

    public string Marker { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public BibleSupplementalTextPlacement Placement { get; set; }

    public int? CharacterOffset { get; set; }
}
