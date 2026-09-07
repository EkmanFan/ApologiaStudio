using ApologiaStudio.Application.Knowledge.DocumentProcessing;

namespace ApologiaStudio.UnitTests.Application.FieldSuggestions;

/// <summary>
/// What a suggestion capability is allowed to see of an editorial draft.
/// </summary>
/// <remarks>
/// The draft's title field is always populated; its provenance says whether it
/// is a title at all. These lock the distinction P2-01 established, on the one
/// path where losing it would silently turn a file name into document
/// metadata.
/// </remarks>
public sealed class EditorialDraftSuggestionEvidenceTests
{
    #region Methods

    [Fact]
    public void An_imported_title_is_evidence()
    {
        var evidence = EditorialDraftSuggestionEvidence.From(
            Draft(
                "Réponse aux objections",
                DocumentManagerEditorialDraftFactory.ImportedTitleOrigin));

        Assert.Equal("Réponse aux objections", evidence.Title);
    }

    [Fact]
    public void A_reviewer_confirmed_title_is_evidence()
    {
        var evidence = EditorialDraftSuggestionEvidence.From(
            Draft(
                "Réponse aux objections",
                DocumentManagerEditorialDraftFactory.EditorialTitleOrigin));

        Assert.Equal("Réponse aux objections", evidence.Title);
    }

    [Fact]
    public void A_file_name_is_never_offered_as_a_title()
    {
        // A file name that reads like a title is still a file name.
        var evidence = EditorialDraftSuggestionEvidence.From(
            Draft(
                "apologetique-tome-2-final-v3",
                DocumentManagerEditorialDraftFactory.FileNameTitleOrigin));

        Assert.Null(evidence.Title);
    }

    [Fact]
    public void A_machine_proposed_title_is_not_evidence_for_another_machine()
    {
        var evidence = EditorialDraftSuggestionEvidence.From(
            Draft(
                "Réponse aux objections",
                DocumentManagerEditorialDraftFactory.MachineProposedTitleOrigin));

        Assert.Null(evidence.Title);
    }

    [Fact]
    public void An_unanticipated_origin_is_not_evidence()
    {
        Assert.Null(
            EditorialDraftSuggestionEvidence.From(Draft("Something", "harvested"))
                .Title);
        Assert.Null(
            EditorialDraftSuggestionEvidence.From(Draft("Something", null!))
                .Title);
    }

    [Fact]
    public void The_description_travels_whatever_the_title_origin_is()
    {
        var evidence = EditorialDraftSuggestionEvidence.From(
            Draft(
                "scan_0012",
                DocumentManagerEditorialDraftFactory.FileNameTitleOrigin,
                "Recueil de sermons prêchés à Genève."));

        Assert.Null(evidence.Title);
        Assert.Equal("Recueil de sermons prêchés à Genève.", evidence.Description);
        Assert.False(evidence.IsEmpty);
    }

    #endregion

    #region Methods Helpers

    private static DocumentManagerEditorialDraft Draft(
        string title,
        string titleOrigin,
        string? description = null) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            new string('a', 64),
            "source.pdf",
            title,
            titleOrigin,
            PrimaryContributorName: null,
            PrimaryContributorRole: null,
            LanguageCode: null,
            EditionStatement: null,
            PublicationYear: null,
            PublicationPlace: null,
            description,
            DocumentManagerEditorialDraftStatus.PendingReview,
            Version: 0,
            LastEditedByUserId: null,
            ReviewedByUserId: null,
            ReviewedAtUtc: null,
            RejectionReason: null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            Parts: [],
            GenreForms: []);

    #endregion
}
