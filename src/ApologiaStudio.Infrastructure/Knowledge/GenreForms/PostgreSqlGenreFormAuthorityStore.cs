using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Application.Knowledge.Ingestion;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure.Knowledge.GenreForms;

/// <summary>
/// Persists an authority snapshot and answers the closed profile queries.
///
/// An authority refresh replaces authority facts only. Apologia profile
/// decisions and Work assignments are never rewritten by an import.
/// </summary>
public sealed class PostgreSqlGenreFormAuthorityStore(
    KnowledgeDbContext context)
    : IGenreFormAuthorityStore
{
    public async Task<GenreFormAuthorityImportResult> ImportAsync(
        GenreFormAuthoritySnapshot snapshot,
        GenreFormAuthorityDataset dataset,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(dataset);

        ValidateDataset(dataset);

        var existing = await context.GenreFormSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Authority == snapshot.Authority &&
                     x.ContentSha256 == snapshot.ContentSha256,
                cancellationToken);

        if (existing is not null)
        {
            return await BuildResultAsync(
                existing.Id,
                snapshot.ContentSha256,
                snapshotAlreadyImported: true,
                dataset,
                cancellationToken);
        }

        await using var transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);

        var snapshotId = KnowledgeStableIds.ForAuthority(
            snapshot.Authority + "/snapshot/" + snapshot.ContentSha256);

        context.GenreFormSnapshots.Add(
            new GenreFormAuthoritySnapshotEntity
            {
                Id = snapshotId,
                Authority = snapshot.Authority,
                SourceUri = snapshot.SourceUri,
                ContentSha256 = snapshot.ContentSha256,
                RetrievedAt = snapshot.RetrievedAt,
                ImporterVersion = snapshot.ImporterVersion,
                TermCount = dataset.Terms.Count
            });

        await context.SaveChangesAsync(cancellationToken);

        await ReplaceAuthorityFactsAsync(
            snapshot,
            snapshotId,
            dataset,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return await BuildResultAsync(
            snapshotId,
            snapshot.ContentSha256,
            snapshotAlreadyImported: false,
            dataset,
            cancellationToken);
    }

    public async Task<IReadOnlyList<GenreFormTermView>> GetSelectableTermsAsync(
        CancellationToken cancellationToken)
    {
        return await (
            from term in context.GenreFormTerms.AsNoTracking()
            join entry in context.GenreFormProfileEntries.AsNoTracking()
                on term.Id equals entry.TermId
            where entry.UsageStatus == "selectable"
            orderby entry.DisplayOrder, term.PreferredLabel
            select new GenreFormTermView(
                term.Id,
                term.AuthorityUri,
                term.AuthorityIdentifier,
                term.PreferredLabel,
                term.AuthorityStatus == "deprecated"
                    ? GenreFormAuthorityStatus.Deprecated
                    : GenreFormAuthorityStatus.Active,
                GenreFormUsageStatus.Selectable,
                entry.DisplayOrder))
            .ToListAsync(cancellationToken);
    }

    public async Task<GenreFormTermView?> GetTermByAuthorityUriAsync(
        string authorityUri,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorityUri);

        var views = await ProjectAsync(
            context.GenreFormTerms
                .AsNoTracking()
                .Where(x => x.AuthorityUri == authorityUri),
            cancellationToken);

        return views.Count == 0 ? null : views[0];
    }

    public async Task<IReadOnlyList<GenreFormTermView>> GetBroaderTermsAsync(
        string authorityUri,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorityUri);

        var termId = await ResolveTermIdAsync(authorityUri, cancellationToken);
        if (termId is null)
        {
            return [];
        }

        var broaderIds = await context.GenreFormBroaderRelations
            .AsNoTracking()
            .Where(x => x.NarrowerTermId == termId.Value)
            .Select(x => x.BroaderTermId)
            .ToListAsync(cancellationToken);

        return await ProjectAsync(
            context.GenreFormTerms
                .AsNoTracking()
                .Where(x => broaderIds.Contains(x.Id)),
            cancellationToken);
    }

    public async Task<IReadOnlyList<GenreFormTermView>> GetNarrowerTermsAsync(
        string authorityUri,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorityUri);

        var termId = await ResolveTermIdAsync(authorityUri, cancellationToken);
        if (termId is null)
        {
            return [];
        }

        // Derived by inverting the persisted broader relation; narrower is
        // never stored as a second source of truth.
        var narrowerIds = await context.GenreFormBroaderRelations
            .AsNoTracking()
            .Where(x => x.BroaderTermId == termId.Value)
            .Select(x => x.NarrowerTermId)
            .ToListAsync(cancellationToken);

        return await ProjectAsync(
            context.GenreFormTerms
                .AsNoTracking()
                .Where(x => narrowerIds.Contains(x.Id)),
            cancellationToken);
    }

    private async Task<Guid?> ResolveTermIdAsync(
        string authorityUri,
        CancellationToken cancellationToken)
    {
        var ids = await context.GenreFormTerms
            .AsNoTracking()
            .Where(x => x.AuthorityUri == authorityUri)
            .Select(x => x.Id)
            .Take(1)
            .ToListAsync(cancellationToken);

        return ids.Count == 0 ? null : ids[0];
    }

    /// <summary>
    /// These read boundaries return a single term or its immediate relatives,
    /// so the profile is composed in memory rather than through a translated
    /// outer join.
    /// </summary>
    private async Task<IReadOnlyList<GenreFormTermView>> ProjectAsync(
        IQueryable<GenreFormAuthorityTermEntity> terms,
        CancellationToken cancellationToken)
    {
        var rows = await terms
            .Select(x => new
            {
                x.Id,
                x.AuthorityUri,
                x.AuthorityIdentifier,
                x.PreferredLabel,
                x.AuthorityStatus
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return [];
        }

        var ids = rows.Select(x => x.Id).ToList();

        var entries = await context.GenreFormProfileEntries
            .AsNoTracking()
            .Where(x => ids.Contains(x.TermId))
            .Select(x => new { x.TermId, x.UsageStatus, x.DisplayOrder })
            .ToListAsync(cancellationToken);

        var byTerm = entries.ToDictionary(x => x.TermId);

        return rows
            .Select(row =>
            {
                byTerm.TryGetValue(row.Id, out var entry);

                return new GenreFormTermView(
                    row.Id,
                    row.AuthorityUri,
                    row.AuthorityIdentifier,
                    row.PreferredLabel,
                    ReadStatus(row.AuthorityStatus),
                    ReadUsage(entry?.UsageStatus),
                    entry?.DisplayOrder);
            })
            .OrderBy(x => x.PreferredLabel, StringComparer.Ordinal)
            .ToList();
    }

    private static GenreFormAuthorityStatus ReadStatus(string value)
    {
        return string.Equals(value, "deprecated", StringComparison.Ordinal)
            ? GenreFormAuthorityStatus.Deprecated
            : GenreFormAuthorityStatus.Active;
    }

    private static GenreFormUsageStatus ReadUsage(string? value)
    {
        return value switch
        {
            "selectable" => GenreFormUsageStatus.Selectable,
            "structural_only" => GenreFormUsageStatus.StructuralOnly,
            _ => GenreFormUsageStatus.Excluded
        };
    }

    private static void ValidateDataset(GenreFormAuthorityDataset dataset)
    {
        var known = dataset.Terms
            .Select(x => x.AuthorityUri)
            .ToHashSet(StringComparer.Ordinal);

        if (known.Count != dataset.Terms.Count)
        {
            throw new GenreFormAuthorityException(
                "The authority dataset contains duplicate term identities.");
        }

        foreach (var term in dataset.Terms)
        {
            foreach (var broader in term.BroaderAuthorityUris)
            {
                if (string.Equals(broader, term.AuthorityUri, StringComparison.Ordinal))
                {
                    throw new GenreFormAuthorityException(
                        $"Term '{term.AuthorityUri}' declares itself as broader.");
                }
            }

            foreach (var related in term.RelatedAuthorityUris)
            {
                if (string.Equals(related, term.AuthorityUri, StringComparison.Ordinal))
                {
                    throw new GenreFormAuthorityException(
                        $"Term '{term.AuthorityUri}' declares itself as related.");
                }
            }
        }
    }

    private async Task ReplaceAuthorityFactsAsync(
        GenreFormAuthoritySnapshot snapshot,
        Guid snapshotId,
        GenreFormAuthorityDataset dataset,
        CancellationToken cancellationToken)
    {
        // Relations and derived facts are rebuilt from the new snapshot.
        // Profile entries and Work assignments are deliberately untouched.
        await context.GenreFormBroaderRelations.ExecuteDeleteAsync(cancellationToken);
        await context.GenreFormRelatedRelations.ExecuteDeleteAsync(cancellationToken);
        await context.GenreFormVariants.ExecuteDeleteAsync(cancellationToken);
        await context.GenreFormNotes.ExecuteDeleteAsync(cancellationToken);

        var existingTerms = await context.GenreFormTerms
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var term in dataset.Terms)
        {
            var status = term.Status == GenreFormAuthorityStatus.Deprecated
                ? "deprecated"
                : "active";

            if (existingTerms.TryGetValue(term.Id, out var entity))
            {
                entity.PreferredLabel = term.PreferredLabel;
                entity.LanguageCode = term.LanguageCode;
                entity.AuthorityStatus = status;
                entity.SnapshotId = snapshotId;
                continue;
            }

            context.GenreFormTerms.Add(
                new GenreFormAuthorityTermEntity
                {
                    Id = term.Id,
                    Authority = snapshot.Authority,
                    AuthorityIdentifier = term.AuthorityIdentifier,
                    AuthorityUri = term.AuthorityUri,
                    PreferredLabel = term.PreferredLabel,
                    LanguageCode = term.LanguageCode,
                    AuthorityStatus = status,
                    SnapshotId = snapshotId
                });
        }

        await context.SaveChangesAsync(cancellationToken);

        var termIds = dataset.Terms
            .ToDictionary(x => x.AuthorityUri, x => x.Id, StringComparer.Ordinal);

        foreach (var term in dataset.Terms)
        {
            foreach (var variant in term.Variants)
            {
                context.GenreFormVariants.Add(
                    new GenreFormAuthorityVariantEntity
                    {
                        TermId = term.Id,
                        Label = variant.Label,
                        LanguageCode = variant.LanguageCode
                    });
            }

            foreach (var note in term.Notes)
            {
                context.GenreFormNotes.Add(
                    new GenreFormAuthorityNoteEntity
                    {
                        TermId = term.Id,
                        NoteType = note.NoteType switch
                        {
                            GenreFormNoteType.History => "history",
                            GenreFormNoteType.Example => "example",
                            _ => "general"
                        },
                        Text = note.Text
                    });
            }
        }

        var broaderPairs = new HashSet<(Guid, Guid)>();
        var relatedPairs = new HashSet<(Guid, Guid)>();

        foreach (var term in dataset.Terms)
        {
            foreach (var broaderUri in term.BroaderAuthorityUris)
            {
                if (!termIds.TryGetValue(broaderUri, out var broaderId))
                {
                    // A dangling reference is authority evidence we cannot
                    // resolve; failing closed is preferred to inventing one.
                    throw new GenreFormAuthorityException(
                        $"Term '{term.AuthorityUri}' declares broader term " +
                        $"'{broaderUri}', which is absent from the snapshot.");
                }

                if (broaderPairs.Add((term.Id, broaderId)))
                {
                    context.GenreFormBroaderRelations.Add(
                        new GenreFormBroaderRelationEntity
                        {
                            NarrowerTermId = term.Id,
                            BroaderTermId = broaderId
                        });
                }
            }

            foreach (var relatedUri in term.RelatedAuthorityUris)
            {
                if (!termIds.TryGetValue(relatedUri, out var relatedId))
                {
                    continue;
                }

                var a = term.Id.CompareTo(relatedId) < 0 ? term.Id : relatedId;
                var b = term.Id.CompareTo(relatedId) < 0 ? relatedId : term.Id;

                if (relatedPairs.Add((a, b)))
                {
                    context.GenreFormRelatedRelations.Add(
                        new GenreFormRelatedRelationEntity
                        {
                            TermIdA = a,
                            TermIdB = b
                        });
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<GenreFormAuthorityImportResult> BuildResultAsync(
        Guid snapshotId,
        string contentSha256,
        bool snapshotAlreadyImported,
        GenreFormAuthorityDataset dataset,
        CancellationToken cancellationToken)
    {
        var published = dataset.Terms
            .Where(x => x.Status == GenreFormAuthorityStatus.Active)
            .Select(x => x.AuthorityUri)
            .ToHashSet(StringComparer.Ordinal);

        var referenced = await (
            from term in context.GenreFormTerms.AsNoTracking()
            join entry in context.GenreFormProfileEntries.AsNoTracking()
                on term.Id equals entry.TermId into entries
            from entry in entries.DefaultIfEmpty()
            let assignments = context.WorkGenreForms
                .Count(x => x.TermId == term.Id)
            where entry != null || assignments > 0
            select new
            {
                term.AuthorityUri,
                term.PreferredLabel,
                term.AuthorityStatus,
                UsageStatus = entry == null ? "excluded" : entry.UsageStatus,
                Assignments = assignments
            })
            .ToListAsync(cancellationToken);

        var review = referenced
            .Where(x => !published.Contains(x.AuthorityUri))
            .Select(x => new GenreFormProfileReviewItem(
                x.AuthorityUri,
                x.PreferredLabel,
                x.AuthorityStatus == "deprecated"
                    ? GenreFormAuthorityStatus.Deprecated
                    : GenreFormAuthorityStatus.Active,
                PresentInSnapshot: false,
                x.UsageStatus switch
                {
                    "selectable" => GenreFormUsageStatus.Selectable,
                    "structural_only" => GenreFormUsageStatus.StructuralOnly,
                    _ => GenreFormUsageStatus.Excluded
                },
                x.Assignments))
            .OrderBy(x => x.PreferredLabel, StringComparer.Ordinal)
            .ToList();

        return new GenreFormAuthorityImportResult(
            snapshotId,
            contentSha256,
            snapshotAlreadyImported,
            dataset.Terms.Count,
            dataset.Terms.Count(x => x.Status == GenreFormAuthorityStatus.Deprecated),
            dataset.Terms.Sum(x => x.Variants.Count),
            dataset.Terms.Sum(x => x.Notes.Count),
            dataset.Terms.Sum(x => x.BroaderAuthorityUris.Count),
            dataset.Terms.Sum(x => x.RelatedAuthorityUris.Count),
            review);
    }
}
