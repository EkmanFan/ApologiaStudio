using System.Text;

namespace ApologiaStudio.BibleCorpusBench;

public static class TextNormalizer
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
