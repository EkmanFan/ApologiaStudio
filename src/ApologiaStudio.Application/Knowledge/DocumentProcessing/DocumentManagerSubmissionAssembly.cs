namespace ApologiaStudio.Application.Knowledge.DocumentProcessing;

public enum DocumentManagerSubmissionAssemblyStatus
{
    AwaitingParts = 0,
    Ready = 1,
    Blocked = 2
}

public sealed record DocumentManagerSubmissionAssembly(
    Guid SubmissionId,
    int ManifestRevision,
    string SourceSha256,
    string OriginalFileName,
    DocumentManagerSubmissionAssemblyStatus Status,
    IReadOnlyList<DocumentManagerSubmissionPart> Parts,
    IReadOnlyList<DocumentManagerSubmissionAssemblyIssue> Issues)
{
    public int ReceivedPartCount =>
        Parts.Count(part => part.ResultReference is not null);

    public int ExpectedPartCount => Parts.Count;
}

public sealed record DocumentManagerSubmissionPart(
    Guid ProcessingUnitId,
    int Ordinal,
    DocumentManagerResultScope Scope,
    string? ResultReference);

public sealed record DocumentManagerSubmissionAssemblyIssue(
    string Code,
    string Message);

public sealed record DocumentManagerStoredResultSummary(
    string ResultReference,
    Guid ProcessingUnitId,
    DocumentManagerResultScope Scope);

public static class DocumentManagerSubmissionAssembler
{
    public static DocumentManagerSubmissionAssembly Assemble(
        DocumentManagerSubmissionManifest manifest,
        IReadOnlyList<DocumentManagerStoredResultSummary> receivedResults)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(receivedResults);

        var issues = new List<DocumentManagerSubmissionAssemblyIssue>();
        ValidatePlan(manifest.ExpectedUnits, issues);

        var expectedById = manifest.ExpectedUnits.ToDictionary(
            unit => unit.ProcessingUnitId);
        var receivedById = new Dictionary<Guid, DocumentManagerStoredResultSummary>();

        foreach (var result in receivedResults)
        {
            if (!expectedById.TryGetValue(result.ProcessingUnitId, out var expected))
            {
                issues.Add(
                    new DocumentManagerSubmissionAssemblyIssue(
                        "unexpected-part",
                        $"Result '{result.ResultReference}' does not belong to manifest revision {manifest.Revision}."));
                continue;
            }

            if (!receivedById.TryAdd(result.ProcessingUnitId, result))
            {
                issues.Add(
                    new DocumentManagerSubmissionAssemblyIssue(
                        "duplicate-part",
                        $"Processing unit '{result.ProcessingUnitId:D}' has more than one stored result."));
                continue;
            }

            if (!ScopesMatch(expected.Scope, result.Scope))
            {
                issues.Add(
                    new DocumentManagerSubmissionAssemblyIssue(
                        "scope-mismatch",
                        $"Processing unit '{result.ProcessingUnitId:D}' does not match its finalized scope."));
            }
        }

        var parts = manifest.ExpectedUnits
            .OrderBy(unit => unit.Ordinal)
            .Select(
                unit =>
                    new DocumentManagerSubmissionPart(
                        unit.ProcessingUnitId,
                        unit.Ordinal,
                        unit.Scope,
                        receivedById.TryGetValue(unit.ProcessingUnitId, out var result)
                            ? result.ResultReference
                            : null))
            .ToArray();

        var status = issues.Count > 0
            ? DocumentManagerSubmissionAssemblyStatus.Blocked
            : parts.All(part => part.ResultReference is not null)
                ? DocumentManagerSubmissionAssemblyStatus.Ready
                : DocumentManagerSubmissionAssemblyStatus.AwaitingParts;

        return new DocumentManagerSubmissionAssembly(
            manifest.SubmissionId,
            manifest.Revision,
            manifest.SourceSha256,
            manifest.OriginalFileName,
            status,
            parts,
            issues);
    }

    private static void ValidatePlan(
        IReadOnlyList<DocumentManagerExpectedProcessingUnit> units,
        ICollection<DocumentManagerSubmissionAssemblyIssue> issues)
    {
        if (units.Count == 1 &&
            units[0].Scope.Kind == "wholeDocument")
        {
            return;
        }

        if (units.All(unit => unit.Scope.Kind == "pageRange"))
        {
            ValidatePageRanges(units, issues);
            return;
        }

        if (units.All(unit => unit.Scope.Kind == "contentUnitRange"))
        {
            ValidateContentUnitRanges(units, issues);
            return;
        }

        issues.Add(
            new DocumentManagerSubmissionAssemblyIssue(
                "incoherent-plan",
                "The finalized manifest mixes incompatible processing scopes."));
    }

    private static void ValidatePageRanges(
        IReadOnlyList<DocumentManagerExpectedProcessingUnit> units,
        ICollection<DocumentManagerSubmissionAssemblyIssue> issues)
    {
        var expectedStart = 1;

        foreach (var unit in units.OrderBy(unit => unit.Ordinal))
        {
            var scope = unit.Scope;

            if (scope.StartPhysicalPageNumber != expectedStart ||
                scope.EndPhysicalPageNumber is null ||
                scope.EndPhysicalPageNumber < expectedStart)
            {
                issues.Add(
                    new DocumentManagerSubmissionAssemblyIssue(
                        "discontinuous-page-ranges",
                        "The finalized physical-page ranges overlap or leave a gap."));
                return;
            }

            expectedStart = checked(scope.EndPhysicalPageNumber.Value + 1);
        }
    }

    private static void ValidateContentUnitRanges(
        IReadOnlyList<DocumentManagerExpectedProcessingUnit> units,
        ICollection<DocumentManagerSubmissionAssemblyIssue> issues)
    {
        var expectedStart = 0;

        foreach (var unit in units.OrderBy(unit => unit.Ordinal))
        {
            var scope = unit.Scope;

            if (scope.StartContentUnitIndex != expectedStart ||
                scope.EndContentUnitIndex is null ||
                scope.EndContentUnitIndex < expectedStart)
            {
                issues.Add(
                    new DocumentManagerSubmissionAssemblyIssue(
                        "discontinuous-content-unit-ranges",
                        "The finalized content-unit ranges overlap or leave a gap."));
                return;
            }

            expectedStart = checked(scope.EndContentUnitIndex.Value + 1);
        }
    }

    private static bool ScopesMatch(
        DocumentManagerResultScope expected,
        DocumentManagerResultScope received) =>
        string.Equals(expected.Kind, received.Kind, StringComparison.Ordinal) &&
        expected.StartPhysicalPageNumber == received.StartPhysicalPageNumber &&
        expected.EndPhysicalPageNumber == received.EndPhysicalPageNumber &&
        string.Equals(expected.Title, received.Title, StringComparison.Ordinal) &&
        expected.StartContentUnitIndex == received.StartContentUnitIndex &&
        string.Equals(expected.StartContentUnitId, received.StartContentUnitId, StringComparison.Ordinal) &&
        expected.EndContentUnitIndex == received.EndContentUnitIndex &&
        string.Equals(expected.EndContentUnitId, received.EndContentUnitId, StringComparison.Ordinal);
}
