namespace ApologiaStudio.Domain.BibleCorpora;

public readonly record struct BibleCorpusVersionId(Guid Value)
{
    public static BibleCorpusVersionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
