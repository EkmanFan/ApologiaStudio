using System.Text;

namespace ApologiaStudio.Infrastructure.BibleCorpora.Ingestion;

internal static class BibleCorpusTextNormalizer
{
    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var result = new StringBuilder(value.Length);
        var pendingSpace = false;

        foreach (var character in value.Normalize(NormalizationForm.FormC))
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = result.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                result.Append(' ');
                pendingSpace = false;
            }

            result.Append(character);
        }

        return result.ToString();
    }
}
