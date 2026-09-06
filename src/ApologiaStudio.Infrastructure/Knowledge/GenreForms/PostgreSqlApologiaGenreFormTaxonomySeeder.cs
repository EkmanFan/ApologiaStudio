using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Infrastructure.Persistence.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace ApologiaStudio.Infrastructure.Knowledge.GenreForms;

/// <summary>
/// Outcome of applying the canonical product taxonomy.
/// </summary>
public sealed record ApologiaGenreFormTaxonomySeedResult(
    string TaxonomyVersion,
    int ActiveTermCount,
    int EncoderPredictableCount,
    int ManualOnlyCount,
    int InsertedCount,
    int UpdatedCount,
    bool Changed);

/// <summary>
/// Applies the canonical Apologia Genre/Form taxonomy to the knowledge store.
/// </summary>
public interface IApologiaGenreFormTaxonomySeeder
{
    /// <summary>
    /// Applies the taxonomy. Deterministic and idempotent; never touches the
    /// external authority catalogue.
    /// </summary>
    Task<ApologiaGenreFormTaxonomySeedResult> ApplyAsync(
        CancellationToken cancellationToken);
}

/// <summary>
/// Seeds the 27 canonical V1 product terms.
/// </summary>
/// <remarks>
/// Identity is derived from the term code, so a re-run reaches the same rows
/// rather than minting duplicates.
///
/// The seeder fails closed on a term that exists under the same code with an
/// incompatible identity. Rewriting such a row would silently re-point every
/// assignment that already referenced it, which is exactly the kind of
/// corruption a seed must refuse rather than repair.
///
/// Descriptive fields — label, definition, order, mode, version — are refreshed,
/// because those are the product's own statements about its own concept. A code
/// and an identity are not.
/// </remarks>
public sealed class PostgreSqlApologiaGenreFormTaxonomySeeder(
    KnowledgeDbContext context,
    TimeProvider timeProvider)
    : IApologiaGenreFormTaxonomySeeder
{
    #region Variables and Constants

    private const string ActiveStatus = "active";

    private const string EncoderPredictableMode = "encoder_predictable";

    private const string ManualOnlyMode = "manual_only";

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<ApologiaGenreFormTaxonomySeedResult> ApplyAsync(
        CancellationToken cancellationToken)
    {
        var canonical = ApologiaGenreFormTaxonomy.Terms;

        GuardCanonicalShape(canonical);

        var existing =
            await context.ApologiaGenreFormTerms
                .Where(x => x.TaxonomyVersion == ApologiaGenreFormTaxonomy.Version)
                .ToListAsync(cancellationToken);

        var byCode = existing.ToDictionary(
            x => x.Code,
            StringComparer.Ordinal);

        var now = timeProvider.GetUtcNow();
        var inserted = 0;
        var updated = 0;

        foreach (var term in canonical)
        {
            var mode = term.PredictionMode == GenreFormPredictionMode.ManualOnly
                ? ManualOnlyMode
                : EncoderPredictableMode;

            if (!byCode.TryGetValue(term.Code, out var row))
            {
                context.ApologiaGenreFormTerms.Add(
                    new ApologiaGenreFormTermEntity
                    {
                        Id = term.Id,
                        Code = term.Code,
                        PreferredLabel = term.PreferredLabel,
                        Definition = term.Definition,
                        PredictionMode = mode,
                        Status = ActiveStatus,
                        TaxonomyVersion = ApologiaGenreFormTaxonomy.Version,
                        DisplayOrder = term.DisplayOrder,
                        UpdatedAtUtc = now
                    });

                inserted++;
                continue;
            }

            if (row.Id != term.Id)
            {
                throw new InvalidOperationException(
                    $"Genre/Form term '{term.Code}' exists with identity " +
                    $"'{row.Id:D}' but the canonical taxonomy derives " +
                    $"'{term.Id:D}'. Refusing to rewrite it: existing " +
                    "assignments would silently change meaning.");
            }

            if (row.PreferredLabel == term.PreferredLabel &&
                row.Definition == term.Definition &&
                row.PredictionMode == mode &&
                row.Status == ActiveStatus &&
                row.DisplayOrder == term.DisplayOrder)
            {
                continue;
            }

            row.PreferredLabel = term.PreferredLabel;
            row.Definition = term.Definition;
            row.PredictionMode = mode;
            row.Status = ActiveStatus;
            row.DisplayOrder = term.DisplayOrder;
            row.UpdatedAtUtc = now;

            updated++;
        }

        var changed = inserted > 0 || updated > 0;

        if (changed)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return await BuildResultAsync(
            inserted,
            updated,
            changed,
            cancellationToken);
    }

    #endregion

    #region Methods Validation

    /// <summary>
    /// Verifies the canonical set before touching the database.
    /// </summary>
    /// <remarks>
    /// A malformed taxonomy must fail before writing, not after: a partially
    /// applied vocabulary is harder to reason about than one that never
    /// applied.
    /// </remarks>
    private static void GuardCanonicalShape(
        IReadOnlyList<ApologiaGenreFormTerm> canonical)
    {
        if (canonical.Select(x => x.Code)
                .Distinct(StringComparer.Ordinal)
                .Count() != canonical.Count)
        {
            throw new InvalidOperationException(
                "The canonical Genre/Form taxonomy contains duplicate codes.");
        }

        if (canonical.Select(x => x.Id).Distinct().Count() != canonical.Count)
        {
            throw new InvalidOperationException(
                "The canonical Genre/Form taxonomy contains duplicate identities.");
        }

        if (canonical.Select(x => x.DisplayOrder).Distinct().Count() != canonical.Count)
        {
            throw new InvalidOperationException(
                "The canonical Genre/Form taxonomy contains duplicate display orders.");
        }
    }

    /// <summary>
    /// Reads the applied taxonomy back and refuses a shape the product does not
    /// declare.
    /// </summary>
    private async Task<ApologiaGenreFormTaxonomySeedResult> BuildResultAsync(
        int inserted,
        int updated,
        bool changed,
        CancellationToken cancellationToken)
    {
        var applied =
            await context.ApologiaGenreFormTerms
                .AsNoTracking()
                .Where(x => x.TaxonomyVersion == ApologiaGenreFormTaxonomy.Version &&
                            x.Status == ActiveStatus)
                .Select(x => x.PredictionMode)
                .ToListAsync(cancellationToken);

        var expectedPredictable =
            ApologiaGenreFormTaxonomy.EncoderPredictableTerms.Count();
        var expectedManual =
            ApologiaGenreFormTaxonomy.ManualOnlyTerms.Count();

        var predictable = applied.Count(
            x => string.Equals(x, EncoderPredictableMode, StringComparison.Ordinal));
        var manual = applied.Count(
            x => string.Equals(x, ManualOnlyMode, StringComparison.Ordinal));

        if (applied.Count != ApologiaGenreFormTaxonomy.Terms.Count ||
            predictable != expectedPredictable ||
            manual != expectedManual)
        {
            throw new InvalidOperationException(
                $"The applied Genre/Form taxonomy is " +
                $"{applied.Count} active / {predictable} encoder-predictable / " +
                $"{manual} manual-only, but the product declares " +
                $"{ApologiaGenreFormTaxonomy.Terms.Count} / " +
                $"{expectedPredictable} / {expectedManual}.");
        }

        return new ApologiaGenreFormTaxonomySeedResult(
            ApologiaGenreFormTaxonomy.Version,
            applied.Count,
            predictable,
            manual,
            inserted,
            updated,
            changed);
    }

    #endregion
}
