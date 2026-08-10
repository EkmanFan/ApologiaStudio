using System.Security.Cryptography;
using System.Text;

namespace ApologiaStudio.KnowledgeImporter;

internal static class StableKnowledgeIds
{
    private const string Root = "apologia-knowledge/v1/";
    private const string Profile = "source/de-decretis-npnf2-04/";

    public static Guid ForProfile(string name) =>
        For(Root + Profile + name);

    public static Guid ForAuthority(string name) =>
        For(Root + "authority/" + name);

    public static Guid ForVocabulary(string name) =>
        For(Root + "vocabulary/" + name);

    private static Guid For(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash.AsSpan(0, 16));
    }
}
