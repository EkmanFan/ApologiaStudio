using System.Security.Cryptography;
using System.Text;

namespace ApologiaStudio.Application.Knowledge.DocumentProcessing;

public static class DocumentManagerEditorialDraftFactory
{
    private const string StableIdPrefix =
        "apologia-document-manager-editorial-draft/v1/";

    public static DocumentManagerEditorialDraft Create(
        DocumentManagerSubmissionAssembly assembly,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        if (assembly.Status != DocumentManagerSubmissionAssemblyStatus.Ready ||
            assembly.Issues.Count != 0 ||
            assembly.ReceivedPartCount != assembly.ExpectedPartCount)
        {
            throw new InvalidOperationException(
                "An editorial draft can only be created from a complete, coherent submission assembly.");
        }

        var title = ProposeTitle(assembly.OriginalFileName);
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
            "original_filename",
            PrimaryContributorName: null,
            PrimaryContributorRole: null,
            LanguageCode: null,
            EditionStatement: null,
            PublicationYear: null,
            PublicationPlace: null,
            Description: null,
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
                .ToArray());
    }

    private static string ProposeTitle(string originalFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);

        var fileName = Path.GetFileName(originalFileName.Trim());
        var proposed = Path.GetFileNameWithoutExtension(fileName).Trim();
        var title = string.IsNullOrWhiteSpace(proposed)
            ? fileName
            : proposed;

        return title.Length <= 1000
            ? title
            : title[..1000];
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
