using ApologiaStudio.Application.Abstractions.FieldSuggestions;

namespace ApologiaStudio.Application.Knowledge.DocumentProcessing;

/// <summary>
/// Builds the bounded evidence a suggestion capability may see for one
/// editorial draft.
/// </summary>
/// <remarks>
/// The draft always carries a title, but not always a documentary one. When no
/// document metadata title existed, the source file name was used as a working
/// value and the draft records that in <c>TitleOrigin</c>.
///
/// Only a title that really came from the document, or that a reviewer entered
/// or confirmed, is offered as title evidence. A file name is not a title
/// merely because it sits in the title field, and passing it on would quietly
/// promote it to document metadata — the exact confusion P2-01 removed. A
/// machine-proposed title is excluded for a different reason: feeding one
/// machine's unconfirmed guess to another manufactures agreement out of
/// nothing.
/// </remarks>
public static class EditorialDraftSuggestionEvidence
{
    #region Methods

    public static FieldSuggestionEvidence From(
        DocumentManagerEditorialDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return new FieldSuggestionEvidence(
            IsDocumentaryTitle(draft.TitleOrigin) ? draft.Title : null,
            draft.Description);
    }

    /// <summary>
    /// A whitelist, so an origin nobody anticipated is never mistaken for a
    /// real title.
    /// </summary>
    private static bool IsDocumentaryTitle(string? titleOrigin) =>
        titleOrigin is
            DocumentManagerEditorialDraftFactory.ImportedTitleOrigin or
            DocumentManagerEditorialDraftFactory.EditorialTitleOrigin;

    #endregion
}
