using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure.Knowledge.GenreForms;

/// <summary>
/// Outcome of applying the approved external alignments.
/// </summary>
public sealed record GenreFormAuthorityMappingSeedResult(
    string Authority,
    int MappingCount,
    int ExactCount,
    int BroaderCount,
    int NarrowerCount,
    int InsertedCount,
    bool Changed);

/// <summary>
/// Applies the approved alignments between the Apologia product taxonomy and
/// an external authority.
/// </summary>
public interface IGenreFormAuthorityMappingSeeder
{
    /// <summary>
    /// Applies the approved LCGFT alignments. Deterministic and idempotent;
    /// never mutates the authority catalogue or the product taxonomy.
    /// </summary>
    Task<GenreFormAuthorityMappingSeedResult> ApplyAsync(
        CancellationToken cancellationToken);
}

/// <summary>
/// Seeds the approved LCGFT alignments.
/// </summary>
/// <remarks>
/// Every declared LCGFT concept is resolved by its authority identifier against
/// the catalogue actually imported, and the resolved row's URI and preferred
/// label must match what was approved. A missing, ambiguous or renamed concept
/// fails the whole seed: an alignment to a concept the authority no longer
/// publishes under that meaning is worse than no alignment at all.
///
/// Nothing is ever repaired in place. A recorded alignment that diverges from
/// the approved one — different identity, different kind, different concept for
/// the same product term — is a semantic disagreement, and rewriting it would
/// hide the disagreement instead of surfacing it.
/// </remarks>
public sealed class PostgreSqlGenreFormAuthorityMappingSeeder(
    KnowledgeDbContext context,
    TimeProvider timeProvider)
    : IGenreFormAuthorityMappingSeeder
{
    #region Methods

    /// <inheritdoc />
    public async Task<GenreFormAuthorityMappingSeedResult> ApplyAsync(
        CancellationToken cancellationToken)
    {
        var approved = ApologiaGenreFormAuthorityMappings.ForAuthority(
            GenreFormAuthority.Lcgft);

        GuardApprovedShape(approved);

        var productTermIds = await ResolveProductTermsAsync(
            approved,
            cancellationToken);

        await ValidateAuthorityConceptsAsync(approved, cancellationToken);

        var existing = await context.GenreFormAuthorityMappings
            .Where(x => x.Authority == GenreFormAuthority.Lcgft)
            .ToListAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        var inserted = 0;

        foreach (var mapping in approved)
        {
            var productTermId = productTermIds[mapping.ProductTermCode];

            var row = existing.FirstOrDefault(
                x => x.ProductTermId == productTermId &&
                     string.Equals(
                         x.ExternalConceptId,
                         mapping.ExternalConceptId,
                         StringComparison.Ordinal));

            if (row is null)
            {
                GuardNoDivergentAlignment(mapping, productTermId, existing);

                context.GenreFormAuthorityMappings.Add(
                    new GenreFormAuthorityMappingEntity
                    {
                        Id = mapping.Id,
                        ProductTermId = productTermId,
                        Authority = mapping.Authority,
                        ExternalConceptId = mapping.ExternalConceptId,
                        ExternalConceptUri = mapping.ExternalConceptUri,
                        MappingKind = Persisted(mapping.MappingKind),
                        UpdatedAtUtc = now
                    });

                inserted++;
                continue;
            }

            GuardRecordedAlignment(mapping, row);
        }

        var changed = inserted > 0;

        if (changed)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return await BuildResultAsync(inserted, changed, cancellationToken);
    }

    #endregion

    #region Methods Resolution

    /// <summary>
    /// Resolves every declared product term against the canonical taxonomy.
    /// </summary>
    private async Task<Dictionary<string, Guid>> ResolveProductTermsAsync(
        IReadOnlyList<ApologiaGenreFormAuthorityMapping> approved,
        CancellationToken cancellationToken)
    {
        var codes = approved.Select(x => x.ProductTermCode).ToList();

        var rows = await context.ApologiaGenreFormTerms
            .AsNoTracking()
            .Where(x => codes.Contains(x.Code))
            .Select(x => new { x.Id, x.Code })
            .ToListAsync(cancellationToken);

        var resolved = rows.ToDictionary(
            x => x.Code,
            x => x.Id,
            StringComparer.Ordinal);

        foreach (var mapping in approved)
        {
            if (!resolved.TryGetValue(mapping.ProductTermCode, out var id))
            {
                throw new InvalidOperationException(
                    $"Product term '{mapping.ProductTermCode}' is absent from " +
                    "the canonical taxonomy, so it cannot be aligned.");
            }

            var expected = ApologiaGenreFormTaxonomy.StableId(
                mapping.ProductTermCode);

            if (id != expected)
            {
                throw new InvalidOperationException(
                    $"Product term '{mapping.ProductTermCode}' is stored with " +
                    $"identity '{id:D}' but the canonical taxonomy derives " +
                    $"'{expected:D}'. Refusing to align to an identity the " +
                    "product does not recognise.");
            }
        }

        return resolved;
    }

    /// <summary>
    /// Verifies that every declared LCGFT concept exists in the imported
    /// catalogue, under exactly the identifier, URI and label approved.
    /// </summary>
    /// <remarks>
    /// The lookup is by authority identifier, never by label. The label is
    /// compared afterwards, as a cross-check: it catches a mistyped identifier
    /// that happens to exist, which an identifier-only lookup would accept.
    /// </remarks>
    private async Task ValidateAuthorityConceptsAsync(
        IReadOnlyList<ApologiaGenreFormAuthorityMapping> approved,
        CancellationToken cancellationToken)
    {
        var identifiers = approved
            .Select(x => x.ExternalConceptId)
            .ToList();

        var candidates = await context.GenreFormTerms
            .AsNoTracking()
            .Where(x => x.Authority == GenreFormAuthority.Lcgft &&
                        identifiers.Contains(x.AuthorityIdentifier))
            .Select(x => new
            {
                x.AuthorityIdentifier,
                x.AuthorityUri,
                x.PreferredLabel
            })
            .ToListAsync(cancellationToken);

        foreach (var mapping in approved)
        {
            var matches = candidates
                .Where(x => string.Equals(
                    x.AuthorityIdentifier,
                    mapping.ExternalConceptId,
                    StringComparison.Ordinal))
                .ToList();

            if (matches.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Approved LCGFT concept '{mapping.ExternalConceptId}' " +
                    $"({mapping.ExternalConceptLabel}) is absent from the " +
                    "imported authority catalogue.");
            }

            if (matches.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Approved LCGFT concept '{mapping.ExternalConceptId}' is " +
                    $"ambiguous: {matches.Count} catalogue rows carry that " +
                    "identifier.");
            }

            var match = matches[0];

            if (!string.Equals(
                    match.AuthorityUri,
                    mapping.ExternalConceptUri,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Approved LCGFT concept '{mapping.ExternalConceptId}' is " +
                    $"published as '{match.AuthorityUri}' but was approved as " +
                    $"'{mapping.ExternalConceptUri}'.");
            }

            if (!string.Equals(
                    match.PreferredLabel,
                    mapping.ExternalConceptLabel,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Approved LCGFT concept '{mapping.ExternalConceptId}' now " +
                    $"reads '{match.PreferredLabel}' but was approved as " +
                    $"'{mapping.ExternalConceptLabel}'. Refusing to align " +
                    "'" + mapping.ProductTermCode + "' to a concept whose " +
                    "meaning may have moved.");
            }
        }
    }

    #endregion

    #region Methods Validation

    /// <summary>
    /// Verifies the approved set before touching the database.
    /// </summary>
    private static void GuardApprovedShape(
        IReadOnlyList<ApologiaGenreFormAuthorityMapping> approved)
    {
        if (approved.Count == 0)
        {
            throw new InvalidOperationException(
                "No LCGFT alignment is approved.");
        }

        if (approved.Select(x => x.Id).Distinct().Count() != approved.Count)
        {
            throw new InvalidOperationException(
                "The approved LCGFT alignments contain duplicate identities.");
        }

        var pairs = approved
            .Select(x => (x.ProductTermCode, x.ExternalConceptId))
            .Distinct()
            .Count();

        if (pairs != approved.Count)
        {
            throw new InvalidOperationException(
                "The approved LCGFT alignments contain a duplicate pair.");
        }
    }

    /// <summary>
    /// Refuses to add a second alignment for a product term already aligned to
    /// a different concept of the same authority.
    /// </summary>
    /// <remarks>
    /// Silently adding one would leave two contradictory alignments in place
    /// and let whichever query ran first decide the meaning.
    /// </remarks>
    private static void GuardNoDivergentAlignment(
        ApologiaGenreFormAuthorityMapping mapping,
        Guid productTermId,
        IReadOnlyList<GenreFormAuthorityMappingEntity> existing)
    {
        var divergent = existing.FirstOrDefault(
            x => x.ProductTermId == productTermId);

        if (divergent is null)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Product term '{mapping.ProductTermCode}' is already aligned to " +
            $"LCGFT concept '{divergent.ExternalConceptId}' but the approved " +
            $"set declares '{mapping.ExternalConceptId}'. Refusing to record " +
            "both: the alignment would become ambiguous.");
    }

    /// <summary>
    /// Refuses a recorded alignment that disagrees with the approved one.
    /// </summary>
    private static void GuardRecordedAlignment(
        ApologiaGenreFormAuthorityMapping mapping,
        GenreFormAuthorityMappingEntity row)
    {
        if (row.Id != mapping.Id)
        {
            throw new InvalidOperationException(
                $"The alignment of '{mapping.ProductTermCode}' to LCGFT " +
                $"'{mapping.ExternalConceptId}' exists with identity " +
                $"'{row.Id:D}' but the approved set derives " +
                $"'{mapping.Id:D}'. Refusing to rewrite it.");
        }

        if (!string.Equals(
                row.MappingKind,
                Persisted(mapping.MappingKind),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The alignment of '{mapping.ProductTermCode}' to LCGFT " +
                $"'{mapping.ExternalConceptId}' is recorded as " +
                $"'{row.MappingKind}' but was approved as " +
                $"'{Persisted(mapping.MappingKind)}'. Refusing to rewrite it: " +
                "a mapping kind is a semantic claim, not a descriptive field.");
        }

        if (!string.Equals(
                row.ExternalConceptUri,
                mapping.ExternalConceptUri,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The alignment of '{mapping.ProductTermCode}' to LCGFT " +
                $"'{mapping.ExternalConceptId}' points at " +
                $"'{row.ExternalConceptUri}' but was approved as " +
                $"'{mapping.ExternalConceptUri}'. Refusing to rewrite it.");
        }
    }

    /// <summary>
    /// Reads the applied alignments back and refuses a shape the product does
    /// not declare.
    /// </summary>
    private async Task<GenreFormAuthorityMappingSeedResult> BuildResultAsync(
        int inserted,
        bool changed,
        CancellationToken cancellationToken)
    {
        var approved = ApologiaGenreFormAuthorityMappings.ForAuthority(
            GenreFormAuthority.Lcgft);

        var applied = await context.GenreFormAuthorityMappings
            .AsNoTracking()
            .Where(x => x.Authority == GenreFormAuthority.Lcgft)
            .Select(x => x.MappingKind)
            .ToListAsync(cancellationToken);

        var exact = applied.Count(
            x => string.Equals(x, "exact", StringComparison.Ordinal));
        var broader = applied.Count(
            x => string.Equals(x, "broader", StringComparison.Ordinal));
        var narrower = applied.Count(
            x => string.Equals(x, "narrower", StringComparison.Ordinal));

        var expectedExact = approved.Count(
            x => x.MappingKind == GenreFormMappingKind.Exact);
        var expectedBroader = approved.Count(
            x => x.MappingKind == GenreFormMappingKind.Broader);

        if (applied.Count != approved.Count ||
            exact != expectedExact ||
            broader != expectedBroader)
        {
            throw new InvalidOperationException(
                $"The applied LCGFT alignments are {applied.Count} rows / " +
                $"{exact} exact / {broader} broader, but the approved set " +
                $"declares {approved.Count} / {expectedExact} / " +
                $"{expectedBroader}.");
        }

        return new GenreFormAuthorityMappingSeedResult(
            GenreFormAuthority.Lcgft,
            applied.Count,
            exact,
            broader,
            narrower,
            inserted,
            changed);
    }

    #endregion

    #region Methods Helpers

    /// <summary>
    /// Gets the persisted form of a mapping kind.
    /// </summary>
    private static string Persisted(
        GenreFormMappingKind mappingKind) =>
        mappingKind switch
        {
            GenreFormMappingKind.Exact => "exact",
            GenreFormMappingKind.Broader => "broader",
            GenreFormMappingKind.Narrower => "narrower",
            GenreFormMappingKind.Close => "close",
            GenreFormMappingKind.Related => "related",
            _ => throw new InvalidOperationException(
                $"Unsupported Genre/Form mapping kind '{mappingKind}'.")
        };

    #endregion
}
