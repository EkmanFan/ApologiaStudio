using System.Text;
using ApologiaStudio.Application.Knowledge.DocumentProcessing;

namespace ApologiaStudio.UnitTests.Application.Knowledge;

/// <summary>
/// Consumption of portable document metadata, and the title it proposes.
/// </summary>
/// <remarks>
/// The correction under test is narrow but functional: the document's own title
/// replaces the file name as the proposal, and the file name survives only as
/// an explicit fallback. The tests therefore assert the provenance as much as
/// the value, because a reviewer needs to know which of the two they are
/// confirming.
/// </remarks>
public sealed class DocumentManagerDocumentMetadataTests
{
    #region Methods Reading

    [Fact]
    public void V5_payload_yields_its_document_metadata()
    {
        var metadata =
            DocumentManagerPortableMetadataReader.Read(
                Payload(
                    "document-processing-result-v5",
                    """
                    ,"documentMetadata":{
                      "title":{"value":"The Case for the Resurrection of Jesus",
                               "origin":"native","sourceHint":"pdf.info.title"},
                      "description":{"value":"Historical apologetics.",
                                     "origin":"native","sourceHint":"pdf.info.subject"}
                    }
                    """));

        Assert.Equal(
            "The Case for the Resurrection of Jesus",
            metadata?.Title);
        Assert.Equal(
            "Historical apologetics.",
            metadata?.Description);
    }

    [Fact]
    public void V5_payload_without_metadata_yields_nothing()
    {
        Assert.Null(
            DocumentManagerPortableMetadataReader.Read(
                Payload("document-processing-result-v5")));
    }

    [Fact]
    public void V5_payload_with_a_blank_title_yields_nothing()
    {
        Assert.Null(
            DocumentManagerPortableMetadataReader.Read(
                Payload(
                    "document-processing-result-v5",
                    """
                    ,"documentMetadata":{"title":{"value":"   ","origin":"native"}}
                    """)));
    }

    [Fact]
    public void V4_payload_carries_no_metadata_and_is_not_corruption()
    {
        // Results published before document metadata existed remain consumable.
        Assert.Null(
            DocumentManagerPortableMetadataReader.Read(
                Payload("document-processing-result-v4")));
    }

    [Fact]
    public void An_unknown_future_schema_fails_closed()
    {
        // Decoding an unknown version as if it were the current one would let a
        // silently changed meaning reach a reviewer as fact.
        var exception =
            Assert.Throws<DocumentManagerResultIntegrityException>(
                () =>
                    DocumentManagerPortableMetadataReader.Read(
                        Payload("document-processing-result-v6")));

        Assert.Contains(
            "document-processing-result-v6",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_payload_without_a_schema_version_fails_closed()
    {
        Assert.Throws<DocumentManagerResultIntegrityException>(
            () =>
                DocumentManagerPortableMetadataReader.Read(
                    Encoding.UTF8.GetBytes("{}")));
    }

    [Fact]
    public void A_malformed_payload_fails_closed()
    {
        Assert.Throws<DocumentManagerResultIntegrityException>(
            () =>
                DocumentManagerPortableMetadataReader.Read(
                    Encoding.UTF8.GetBytes("not json")));
    }

    #endregion

    #region Methods Title Proposal

    [Fact]
    public void A_document_title_is_preferred_over_the_file_name()
    {
        var draft =
            DocumentManagerEditorialDraftFactory.Create(
                Assembly(),
                CreatedAt,
                new DocumentManagerDocumentMetadata(
                    "The Case for the Resurrection of Jesus",
                    null));

        Assert.Equal(
            "The Case for the Resurrection of Jesus",
            draft.Title);
        Assert.Equal(
            DocumentManagerEditorialDraftFactory.ImportedTitleOrigin,
            draft.TitleOrigin);
    }

    [Fact]
    public void A_document_title_wins_even_when_it_differs_from_the_file_name()
    {
        // The file name here reads perfectly well as a title. It still loses.
        var draft =
            DocumentManagerEditorialDraftFactory.Create(
                Assembly(
                    "Vers une écologie des émotions.pdf"),
                CreatedAt,
                new DocumentManagerDocumentMetadata(
                    "Toward an Ecology of Emotions",
                    null));

        Assert.Equal(
            "Toward an Ecology of Emotions",
            draft.Title);
        Assert.Equal(
            "Vers une écologie des émotions.pdf",
            draft.OriginalFileName);
    }

    [Fact]
    public void No_document_title_falls_back_to_the_file_name()
    {
        var draft =
            DocumentManagerEditorialDraftFactory.Create(
                Assembly(),
                CreatedAt,
                new DocumentManagerDocumentMetadata(
                    null,
                    "A description without a title."));

        Assert.Equal(
            "habermas-resurrection",
            draft.Title);
        Assert.Equal(
            DocumentManagerEditorialDraftFactory.FileNameTitleOrigin,
            draft.TitleOrigin);
    }

    [Fact]
    public void Absent_metadata_falls_back_to_the_file_name()
    {
        // The v4 path, and any result whose source stated nothing.
        var draft =
            DocumentManagerEditorialDraftFactory.Create(
                Assembly(),
                CreatedAt);

        Assert.Equal(
            "habermas-resurrection",
            draft.Title);
        Assert.Equal(
            DocumentManagerEditorialDraftFactory.FileNameTitleOrigin,
            draft.TitleOrigin);
    }

    [Fact]
    public void A_blank_document_title_falls_back_rather_than_proposing_nothing()
    {
        var draft =
            DocumentManagerEditorialDraftFactory.Create(
                Assembly(),
                CreatedAt,
                new DocumentManagerDocumentMetadata(
                    "   ",
                    null));

        Assert.Equal(
            "habermas-resurrection",
            draft.Title);
        Assert.Equal(
            DocumentManagerEditorialDraftFactory.FileNameTitleOrigin,
            draft.TitleOrigin);
    }

    #endregion

    #region Methods Description Proposal

    [Fact]
    public void A_document_description_is_proposed_when_present()
    {
        Assert.Equal(
            "A study of eyewitness testimony.",
            DocumentManagerEditorialDraftFactory.Create(
                    Assembly(),
                    CreatedAt,
                    new DocumentManagerDocumentMetadata(
                        null,
                        "A study of eyewitness testimony."))
                .Description);
    }

    [Fact]
    public void No_description_is_fabricated_when_the_source_states_none()
    {
        Assert.Null(
            DocumentManagerEditorialDraftFactory.Create(
                    Assembly(),
                    CreatedAt)
                .Description);
    }

    #endregion

    #region Methods Helpers

    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    private static byte[] Payload(
        string schemaVersion,
        string? extra = null) =>
        Encoding.UTF8.GetBytes(
            $$"""{"schemaVersion":"{{schemaVersion}}"{{extra}}}""");

    private static DocumentManagerSubmissionAssembly Assembly(
        string originalFileName = "habermas-resurrection.pdf") =>
        new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ManifestRevision: 1,
            "9f2c4b8e1d7a306f5c9b2e8a4d1f7c30596b8e2a4d1f7c30596b8e2a4d1f7c30",
            originalFileName,
            DocumentManagerSubmissionAssemblyStatus.Ready,
            [
                new DocumentManagerSubmissionPart(
                    Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Ordinal: 0,
                    new DocumentManagerResultScope(
                        "physical_page_range",
                        1,
                        10,
                        null,
                        null,
                        null,
                        null,
                        null),
                    "result-reference-1")
            ],
            []);

    #endregion
}
