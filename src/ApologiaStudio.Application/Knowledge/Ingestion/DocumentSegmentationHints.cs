namespace ApologiaStudio.Application.Knowledge.Ingestion;

public sealed record DocumentSegmentationHints(
    IReadOnlyList<HeadingSegmentKindHint> HeadingSegmentKinds)
{
    public static DocumentSegmentationHints Empty { get; } =
        new(Array.Empty<HeadingSegmentKindHint>());
}
