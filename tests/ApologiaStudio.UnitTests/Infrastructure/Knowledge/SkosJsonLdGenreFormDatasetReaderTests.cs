using System.Text;
using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Infrastructure.Knowledge.GenreForms;

namespace ApologiaStudio.UnitTests.Infrastructure.Knowledge;

public sealed class SkosJsonLdGenreFormDatasetReaderTests
{
    private const string Base = "http://id.loc.gov/authorities/genreForms/";

    [Fact]
    public void Read_ShouldTakeCanonicalUriFromConceptNode()
    {
        // The record envelope carries a relative identifier; only the concept
        // node holds the canonical absolute URI.
        var dataset = Read(
            Record(
                envelopeId: "/authorities/genreForms/gf1",
                Concept("gf1", "Sermons")));

        var term = Assert.Single(dataset.Terms);
        Assert.Equal(Base + "gf1", term.AuthorityUri);
        Assert.Equal("gf1", term.AuthorityIdentifier);
        Assert.Equal("Sermons", term.PreferredLabel);
    }

    [Fact]
    public void Read_ShouldPreserveMultipleBroaderTerms()
    {
        var dataset = Read(
            Record(
                "/authorities/genreForms/gf1",
                Concept(
                    "gf1",
                    "Sermons",
                    broader: ["gf2", "gf3"])));

        var term = Assert.Single(dataset.Terms);
        Assert.Equal(
            [Base + "gf2", Base + "gf3"],
            term.BroaderAuthorityUris);
    }

    [Fact]
    public void Read_ShouldKeepNoteSemanticsDistinct()
    {
        var dataset = Read(
            Record(
                "/authorities/genreForms/gf1",
                $$"""
                  {
                    "@id": "{{Base}}gf1",
                    "@type": "skos:Concept",
                    "skos:prefLabel": {"@language": "en", "@value": "Creeds"},
                    "skos:note": {"@language": "en", "@value": "general note"},
                    "skos:historyNote": {"@language": "en", "@value": "history note"},
                    "skos:example": {"@language": "en", "@value": "example note"}
                  }
                  """));

        var notes = Assert.Single(dataset.Terms).Notes;

        Assert.Equal(3, notes.Count);
        Assert.Contains(
            notes,
            x => x.NoteType == GenreFormNoteType.General && x.Text == "general note");
        Assert.Contains(
            notes,
            x => x.NoteType == GenreFormNoteType.History && x.Text == "history note");
        Assert.Contains(
            notes,
            x => x.NoteType == GenreFormNoteType.Example && x.Text == "example note");
    }

    [Fact]
    public void Read_ShouldSkipRecordWithoutConceptNode()
    {
        // A term withdrawn upstream keeps only its change-set records.
        var dataset = Read(
            Record(
                "/authorities/genreForms/gf1",
                Concept("gf1", "Sermons")),
            Record(
                "/authorities/genreForms/gf9",
                """
                {
                  "@id": "_:n1",
                  "@type": "cs:ChangeSet",
                  "cs:changeReason": "deprecated"
                }
                """));

        var term = Assert.Single(dataset.Terms);
        Assert.Equal(Base + "gf1", term.AuthorityUri);
    }

    [Fact]
    public void Read_ShouldMarkConceptWithDeprecationEvidence()
    {
        var dataset = Read(
            Record(
                "/authorities/genreForms/gf1",
                Concept("gf1", "Sermons"),
                """
                {
                  "@id": "_:n1",
                  "@type": "cs:ChangeSet",
                  "cs:changeReason": "deprecated"
                }
                """));

        Assert.Equal(
            GenreFormAuthorityStatus.Deprecated,
            Assert.Single(dataset.Terms).Status);
    }

    [Fact]
    public void Read_ShouldRejectDuplicateAuthorityIdentity()
    {
        var exception = Assert.Throws<GenreFormAuthorityException>(
            () => Read(
                Record("/authorities/genreForms/gf1", Concept("gf1", "Sermons")),
                Record("/authorities/genreForms/gf1", Concept("gf1", "Sermons"))));

        Assert.Contains("more than once", exception.Message);
    }

    [Fact]
    public void Read_ShouldRejectConceptWithoutPreferredLabel()
    {
        Assert.Throws<GenreFormAuthorityException>(
            () => Read(
                Record(
                    "/authorities/genreForms/gf1",
                    $$"""
                      {
                        "@id": "{{Base}}gf1",
                        "@type": "skos:Concept"
                      }
                      """)));
    }

    [Fact]
    public void Read_ShouldRejectMalformedJson()
    {
        Assert.Throws<GenreFormAuthorityException>(
            () => Read("{ not json"));
    }

    [Fact]
    public void Read_ShouldRejectEmptyDataset()
    {
        Assert.Throws<GenreFormAuthorityException>(() => Read());
    }

    private static GenreFormAuthorityDataset Read(params string[] lines)
    {
        var payload = string.Join("\n", lines);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        return new SkosJsonLdGenreFormDatasetReader().Read(stream);
    }

    private static string Record(string envelopeId, params string[] nodes)
    {
        return $$"""
                 {"@id": "{{envelopeId}}", "@graph": [{{string.Join(",", nodes)}}]}
                 """.ReplaceLineEndings(" ");
    }

    private static string Concept(
        string identifier,
        string label,
        string[]? broader = null,
        string[]? related = null)
    {
        var parts = new List<string>
        {
            $"\"@id\": \"{Base}{identifier}\"",
            "\"@type\": \"skos:Concept\"",
            $"\"skos:prefLabel\": {{\"@language\": \"en\", \"@value\": \"{label}\"}}"
        };

        if (broader is { Length: > 0 })
        {
            parts.Add(
                "\"skos:broader\": [" +
                string.Join(",", broader.Select(x => $"{{\"@id\": \"{Base}{x}\"}}")) +
                "]");
        }

        if (related is { Length: > 0 })
        {
            parts.Add(
                "\"skos:related\": [" +
                string.Join(",", related.Select(x => $"{{\"@id\": \"{Base}{x}\"}}")) +
                "]");
        }

        return "{" + string.Join(",", parts) + "}";
    }
}
