using System.Security.Cryptography;
using System.Text;

namespace ApologiaStudio.Application.Knowledge.Ingestion;

public static class KnowledgeStableIds
{
    private const string Root = "apologia-knowledge/v1/";

    public static Guid ForSourceProfile(
        string stableIdNamespace,
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            stableIdNamespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (stableIdNamespace.IndexOfAny(['/', '\\']) >= 0)
        {
            throw new ArgumentException(
                "The stable source namespace must be a single path component.",
                nameof(stableIdNamespace));
        }

        return For(
            Root +
            "source/" +
            stableIdNamespace +
            "/" +
            name);
    }

    public static Guid ForAuthority(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return For(Root + "authority/" + name);
    }

    public static Guid ForVocabulary(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return For(Root + "vocabulary/" + name);
    }

    private static Guid For(string value)
    {
        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(value));

        return new Guid(hash.AsSpan(0, 16));
    }
}
