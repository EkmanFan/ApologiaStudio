using System.Text.Json;
using ApologiaStudio.Application.Knowledge.GenreForms;

namespace ApologiaStudio.Infrastructure.Knowledge.GenreForms;

/// <summary>
/// Reads the Library of Congress SKOS/RDF JSON-LD bulk representation into the
/// serialization-agnostic authority model.
///
/// The dataset is JSON Lines: one record per line, each holding an
/// <c>@graph</c> of nodes. Only this adapter knows about SKOS property names.
/// </summary>
public sealed class SkosJsonLdGenreFormDatasetReader
    : IGenreFormAuthorityDatasetReader
{
    private const string ConceptType = "skos:Concept";
    private const string DeprecatedChangeReason = "deprecated";

    public string RepresentationId => "lcgft-skosrdf-jsonld-v1";

    public GenreFormAuthorityDataset Read(Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var terms = new List<GenreFormAuthorityTerm>();
        var seenUris = new HashSet<string>(StringComparer.Ordinal);

        using var reader = new StreamReader(content);

        var lineNumber = 0;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException exception)
            {
                throw new GenreFormAuthorityException(
                    $"Authority record on line {lineNumber} is not valid JSON.",
                    exception);
            }

            using (document)
            {
                var term = ReadRecord(document.RootElement, lineNumber);
                if (term is null)
                {
                    continue;
                }

                if (!seenUris.Add(term.AuthorityUri))
                {
                    throw new GenreFormAuthorityException(
                        "Authority identity is ambiguous: " +
                        $"'{term.AuthorityUri}' appears more than once.");
                }

                terms.Add(term);
            }
        }

        if (terms.Count == 0)
        {
            throw new GenreFormAuthorityException(
                "The authority dataset contains no usable concept.");
        }

        return new GenreFormAuthorityDataset(terms);
    }

    private static GenreFormAuthorityTerm? ReadRecord(
        JsonElement record,
        int lineNumber)
    {
        if (!record.TryGetProperty("@graph", out var graph) ||
            graph.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        // A concept withdrawn upstream keeps only its change-set nodes, so the
        // absence of a concept node is itself the deprecation evidence.
        var concept = FindConceptNode(graph);
        var deprecated = HasDeprecationEvidence(graph);

        if (concept is null)
        {
            return null;
        }

        var conceptNode = concept.Value;
        var authorityUri = ReadId(conceptNode);

        if (string.IsNullOrWhiteSpace(authorityUri))
        {
            throw new GenreFormAuthorityException(
                $"Authority concept on line {lineNumber} has no identifier.");
        }

        if (!Uri.TryCreate(authorityUri, UriKind.Absolute, out _))
        {
            throw new GenreFormAuthorityException(
                $"Authority concept identity '{authorityUri}' on line " +
                $"{lineNumber} is not an absolute URI.");
        }

        var preferredLabel = ReadLabelValue(conceptNode, "skos:prefLabel");
        if (string.IsNullOrWhiteSpace(preferredLabel))
        {
            throw new GenreFormAuthorityException(
                $"Authority concept '{authorityUri}' has no preferred label.");
        }

        return new GenreFormAuthorityTerm(
            authorityUri,
            ReadIdentifier(authorityUri),
            preferredLabel,
            ReadLabelLanguage(conceptNode, "skos:prefLabel"),
            deprecated
                ? GenreFormAuthorityStatus.Deprecated
                : GenreFormAuthorityStatus.Active,
            ReadVariants(conceptNode),
            ReadNotes(conceptNode),
            ReadReferences(conceptNode, "skos:broader"),
            ReadReferences(conceptNode, "skos:related"));
    }

    private static JsonElement? FindConceptNode(JsonElement graph)
    {
        foreach (var node in graph.EnumerateArray())
        {
            if (node.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!HasType(node, ConceptType))
            {
                continue;
            }

            // The record envelope carries a relative @id; only the concept node
            // holds the canonical absolute authority URI.
            var id = ReadId(node);
            if (!string.IsNullOrWhiteSpace(id) &&
                Uri.TryCreate(id, UriKind.Absolute, out _))
            {
                return node;
            }
        }

        return null;
    }

    private static bool HasDeprecationEvidence(JsonElement graph)
    {
        foreach (var node in graph.EnumerateArray())
        {
            if (node.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (node.TryGetProperty("cs:changeReason", out var reason) &&
                reason.ValueKind == JsonValueKind.String &&
                string.Equals(
                    reason.GetString(),
                    DeprecatedChangeReason,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasType(JsonElement node, string expected)
    {
        if (!node.TryGetProperty("@type", out var type))
        {
            return false;
        }

        if (type.ValueKind == JsonValueKind.String)
        {
            return string.Equals(type.GetString(), expected, StringComparison.Ordinal);
        }

        if (type.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var candidate in type.EnumerateArray())
        {
            if (candidate.ValueKind == JsonValueKind.String &&
                string.Equals(candidate.GetString(), expected, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ReadId(JsonElement node)
    {
        return node.TryGetProperty("@id", out var id) &&
               id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null;
    }

    private static string ReadIdentifier(string authorityUri)
    {
        var index = authorityUri.LastIndexOf('/');
        return index >= 0 && index < authorityUri.Length - 1
            ? authorityUri[(index + 1)..]
            : authorityUri;
    }

    private static string? ReadLabelValue(JsonElement node, string property)
    {
        foreach (var element in EnumerateValues(node, property))
        {
            var value = ReadValue(element);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? ReadLabelLanguage(JsonElement node, string property)
    {
        foreach (var element in EnumerateValues(node, property))
        {
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty("@language", out var language) &&
                language.ValueKind == JsonValueKind.String)
            {
                return language.GetString();
            }
        }

        return null;
    }

    private static IReadOnlyList<GenreFormAuthorityVariant> ReadVariants(
        JsonElement node)
    {
        var variants = new List<GenreFormAuthorityVariant>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var element in EnumerateValues(node, "skos:altLabel"))
        {
            var value = ReadValue(element);
            if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
            {
                continue;
            }

            string? language = null;
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty("@language", out var languageElement) &&
                languageElement.ValueKind == JsonValueKind.String)
            {
                language = languageElement.GetString();
            }

            variants.Add(new GenreFormAuthorityVariant(value, language));
        }

        return variants;
    }

    private static IReadOnlyList<GenreFormAuthorityNote> ReadNotes(
        JsonElement node)
    {
        var notes = new List<GenreFormAuthorityNote>();

        AddNotes(node, "skos:note", GenreFormNoteType.General, notes);
        AddNotes(node, "skos:historyNote", GenreFormNoteType.History, notes);
        AddNotes(node, "skos:example", GenreFormNoteType.Example, notes);

        return notes;
    }

    private static void AddNotes(
        JsonElement node,
        string property,
        GenreFormNoteType noteType,
        List<GenreFormAuthorityNote> notes)
    {
        foreach (var element in EnumerateValues(node, property))
        {
            var value = ReadValue(element);
            if (!string.IsNullOrWhiteSpace(value))
            {
                notes.Add(new GenreFormAuthorityNote(noteType, value));
            }
        }
    }

    private static IReadOnlyList<string> ReadReferences(
        JsonElement node,
        string property)
    {
        var references = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var element in EnumerateValues(node, property))
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = ReadId(element);
            if (string.IsNullOrWhiteSpace(id) ||
                !Uri.TryCreate(id, UriKind.Absolute, out _))
            {
                continue;
            }

            if (seen.Add(id))
            {
                references.Add(id);
            }
        }

        return references;
    }

    private static IEnumerable<JsonElement> EnumerateValues(
        JsonElement node,
        string property)
    {
        if (!node.TryGetProperty(property, out var value))
        {
            yield break;
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in value.EnumerateArray())
            {
                yield return element;
            }

            yield break;
        }

        yield return value;
    }

    private static string? ReadValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Object when element.TryGetProperty("@value", out var value) &&
                                      value.ValueKind == JsonValueKind.String =>
                value.GetString(),
            _ => null
        };
    }
}
