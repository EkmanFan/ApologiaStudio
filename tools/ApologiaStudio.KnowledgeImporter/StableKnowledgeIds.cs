using ApologiaStudio.Application.Knowledge.Ingestion;

namespace ApologiaStudio.KnowledgeImporter;

internal static class StableKnowledgeIds
{
    private const string LegacyProfileNamespace =
        "de-decretis-npnf2-04";

    public static Guid ForProfile(string name) =>
        KnowledgeStableIds.ForSourceProfile(
            LegacyProfileNamespace,
            name);

    public static Guid ForSourceProfile(
        string stableIdNamespace,
        string name) =>
        KnowledgeStableIds.ForSourceProfile(
            stableIdNamespace,
            name);

    public static Guid ForAuthority(string name) =>
        KnowledgeStableIds.ForAuthority(name);

    public static Guid ForVocabulary(string name) =>
        KnowledgeStableIds.ForVocabulary(name);
}
