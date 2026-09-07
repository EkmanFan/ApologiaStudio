using System.Security.Cryptography;
using System.Text;

namespace ApologiaStudio.Application.Knowledge.DocumentProcessing;

public static class DocumentManagerEditorialDraftFactory
{
    private const string StableIdPrefix =
        "apologia-document-manager-editorial-draft/v1/";

    /// <summary>
    /// Title provenance: the value came from the portable document metadata.
    /// </summary>
    public const string ImportedTitleOrigin = "imported";

    /// <summary>
    /// Title provenance: no document metadata title existed, so the source file
    /// name was used.
    /// </summary>
    public const string FileNameTitleOrigin = "original_filename";

    /// <summary>
    /// Title provenance: a reviewer entered or confirmed the value.
    /// </summary>
    public const string EditorialTitleOrigin = "editorial";

    /// <summary>
    /// Title provenance: a machine proposed the value and no reviewer has
    /// confirmed it.
    /// </summary>
    public const string MachineProposedTitleOrigin = "ai_proposed";

    private const int MaximumTitleLength = 1000;

    public static DocumentManagerEditorialDraft Create(
        DocumentManagerSubmissionAssembly assembly,
        DateTimeOffset createdAtUtc,
        DocumentManagerDocumentMetadata? documentMetadata = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        if (assembly.Status != DocumentManagerSubmissionAssemblyStatus.Ready ||
            assembly.Issues.Count != 0 ||
            assembly.ReceivedPartCount != assembly.ExpectedPartCount)
        {
            throw new InvalidOperationException(
                "An editorial draft can only be created from a complete, coherent submission assembly.");
        }

        // The document's own title wins over the file that carried it, even
        // when the two differ. A file name that reads like a title is still a
        // file name, and only the reviewer can promote it.
        var proposedTitle = Truncate(documentMetadata?.Title);
        var titleOrigin = proposedTitle is null
            ? FileNameTitleOrigin
            : ImportedTitleOrigin;

        var title = proposedTitle ?? ProposeTitle(assembly.OriginalFileName);
        var draftId = CreateStableId(
            assembly.SubmissionId,
            assembly.ManifestRevision);

        return new DocumentManagerEditorialDraft(
            draftId,
            assembly.SubmissionId,
            assembly.ManifestRevision,
            assembly.SourceSha256.ToLowerInvariant(),
            assembly.OriginalFileName,
            title,
            titleOrigin,
            PrimaryContributorName: null,
            PrimaryContributorRole: null,
            LanguageCode: null,
            EditionStatement: null,
            PublicationYear: null,
            PublicationPlace: null,
            Description: Truncate(documentMetadata?.Description),
            DocumentManagerEditorialDraftStatus.PendingReview,
            Version: 0,
            LastEditedByUserId: null,
            ReviewedByUserId: null,
            ReviewedAtUtc: null,
            RejectionReason: null,
            createdAtUtc,
            createdAtUtc,
            assembly.Parts
                .OrderBy(part => part.Ordinal)
                .Select(
                    part =>
                        new DocumentManagerEditorialDraftPart(
                            part.ProcessingUnitId,
                            part.Ordinal,
                            part.ResultReference!,
                            part.Scope))
                .ToArray(),
            // A new draft carries no genre/form: the reviewer chooses.
            GenreForms: []);
    }

    /// <summary>
    /// Normalizes a proposed value to the application's length constraint, and
    /// treats a blank value as no value at all.
    /// </summary>
    private static string? Truncate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= MaximumTitleLength
            ? trimmed
            : trimmed[..MaximumTitleLength];
    }

    private static string ProposeTitle(string originalFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);

        var fileName = Path.GetFileName(originalFileName.Trim());
        var proposed = Path.GetFileNameWithoutExtension(fileName).Trim();
        var title = string.IsNullOrWhiteSpace(proposed)
            ? fileName
            : proposed;

        return title.Length <= MaximumTitleLength
            ? title
            : title[..MaximumTitleLength];
    }

    private static Guid CreateStableId(
        Guid submissionId,
        int manifestRevision)
    {
        var value =
            StableIdPrefix +
            submissionId.ToString("N") +
            "/" +
            manifestRevision;
        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(value));

        return new Guid(hash.AsSpan(0, 16));
    }
}
