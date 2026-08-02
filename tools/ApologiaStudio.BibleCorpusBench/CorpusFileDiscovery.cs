namespace ApologiaStudio.BibleCorpusBench;

internal static class CorpusFileDiscovery
{
    public static IReadOnlyList<string> Find(
        string path,
        IReadOnlySet<string> allowedExtensions,
        string formatName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath))
        {
            if (!allowedExtensions.Contains(Path.GetExtension(fullPath)))
            {
                var extensions = string.Join(", ", allowedExtensions.OrderBy(value => value));
                throw new BibleCorpusException(
                    $"{formatName} file must use one of these extensions: {extensions}. File: {fullPath}");
            }

            return new[] { fullPath };
        }

        if (!Directory.Exists(fullPath))
        {
            throw new BibleCorpusException($"{formatName} path does not exist: {fullPath}");
        }

        var files = Directory
            .EnumerateFiles(fullPath, "*", SearchOption.AllDirectories)
            .Where(file => allowedExtensions.Contains(Path.GetExtension(file)))
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToArray();

        if (files.Length == 0)
        {
            var extensions = string.Join(", ", allowedExtensions.OrderBy(value => value));
            throw new BibleCorpusException(
                $"No {formatName} files with extensions {extensions} were found under {fullPath}.");
        }

        return files;
    }
}
