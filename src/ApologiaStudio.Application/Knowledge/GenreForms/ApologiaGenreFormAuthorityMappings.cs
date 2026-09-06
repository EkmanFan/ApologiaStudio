using System.Security.Cryptography;
using System.Text;

namespace ApologiaStudio.Application.Knowledge.GenreForms;

/// <summary>
/// How an Apologia product term relates to an external authority concept.
/// </summary>
/// <remarks>
/// The direction is fixed and is never read the other way round:
///
/// <code>Apologia product term → external authority concept</code>
///
/// <list type="bullet">
/// <item><see cref="Exact"/> — the two concepts are semantically equivalent.</item>
/// <item><see cref="Broader"/> — the <b>Apologia</b> concept is wider than the
/// external one, so the external concept falls inside it.</item>
/// <item><see cref="Narrower"/> — the <b>Apologia</b> concept is tighter than
/// the external one, so it falls inside the external concept.</item>
/// <item><see cref="Close"/> — near but not equivalent, with no usable
/// inclusion either way.</item>
/// <item><see cref="Related"/> — associative only; no inclusion is claimed.</item>
/// </list>
///
/// Stating it once here is not decoration: a reversed reading of
/// <see cref="Broader"/> silently inverts every inference later drawn from the
/// alignment. The convention is locked by a test.
/// </remarks>
public enum GenreFormMappingKind
{
    /// <summary>Semantically equivalent concepts.</summary>
    Exact = 0,

    /// <summary>The Apologia concept is wider than the external concept.</summary>
    Broader = 1,

    /// <summary>The Apologia concept is tighter than the external concept.</summary>
    Narrower = 2,

    /// <summary>Close but not equivalent, with no usable inclusion.</summary>
    Close = 3,

    /// <summary>Associative relation without inclusion.</summary>
    Related = 4
}

/// <summary>
/// The external vocabularies Apologia may align to.
/// </summary>
public static class GenreFormAuthority
{
    /// <summary>Library of Congress Genre/Form Terms.</summary>
    public const string Lcgft = "lcgft";

    /// <summary>Bibliothèque nationale de France.</summary>
    public const string Bnf = "bnf";
}

/// <summary>
/// One approved alignment between an Apologia product term and an external
/// authority concept.
/// </summary>
/// <remarks>
/// <see cref="ExternalConceptId"/> is the authority's own stable identifier and
/// is what carries the alignment. <see cref="ExternalConceptLabel"/> exists for
/// human audit and for a fail-closed cross-check at seed time; it is never the
/// key, and it is not persisted.
/// </remarks>
public sealed record ApologiaGenreFormAuthorityMapping(
    string ProductTermCode,
    string Authority,
    string ExternalConceptId,
    string? ExternalConceptUri,
    string ExternalConceptLabel,
    GenreFormMappingKind MappingKind)
{
    /// <summary>
    /// Gets the stable identity of the alignment.
    /// </summary>
    public Guid Id =>
        ApologiaGenreFormAuthorityMappings.StableId(
            Authority,
            ProductTermCode,
            ExternalConceptId);
}

/// <summary>
/// The approved external alignments of the canonical product taxonomy.
/// </summary>
/// <remarks>
/// Alignment is recorded, never inferred. A product term with no approved
/// alignment simply has none: an absent mapping is a valid and expected state,
/// and inventing one to fill the table would assert an equivalence nobody
/// approved.
///
/// LCGFT identifiers are declared here rather than resolved from a label,
/// because a preferred label is a translation-and-revision surface while the
/// authority identifier is the stable fact. The label travels with the
/// declaration only so the seeder can refuse an identifier that no longer
/// carries the concept it was approved for.
///
/// No BnF alignment is declared: the repository holds no authoritative BnF
/// mapping, and reconstructing one from memory would fabricate a bibliographic
/// claim. The model accepts BnF; V1 has nothing true to put in it.
/// </remarks>
public static class ApologiaGenreFormAuthorityMappings
{
    #region Variables and Constants

    /// <summary>
    /// Namespace for deriving an alignment's stable identity.
    /// </summary>
    /// <remarks>
    /// Identity is the triple (authority, product term, external concept),
    /// which is exactly the alignment being asserted. It carries no taxonomy
    /// version, for the same reason a term's identity does not.
    /// </remarks>
    private const string IdentityNamespace =
        "apologia-genre-form-authority-mapping/";

    /// <summary>
    /// The twelve approved LCGFT alignments of Apologia Genre/Form V1.
    /// </summary>
    /// <remarks>
    /// Two are <see cref="GenreFormMappingKind.Broader"/> and the reading is
    /// always product-first:
    ///
    /// <list type="bullet">
    /// <item><c>creed</c> covers any explicit profession of belief, religious
    /// or not, while LCGFT <i>Creeds</i> is confined to religious ones.</item>
    /// <item><c>academic_degree_work</c> covers any degree-qualifying academic
    /// work, while LCGFT <i>Academic theses</i> is confined to theses.</item>
    /// </list>
    ///
    /// LCGFT <i>Pastoral letters and charges</i> and <i>Hagiographies</i> are
    /// deliberately unmapped: no V1 product term corresponds to them.
    /// </remarks>
    public static IReadOnlyList<ApologiaGenreFormAuthorityMapping> Approved { get; } =
    [
        Lcgft(
            "apologetic_writing",
            "gf2015026027",
            "Apologetic writings",
            GenreFormMappingKind.Exact),
        Lcgft(
            "textbook",
            "gf2014026191",
            "Textbooks",
            GenreFormMappingKind.Exact),
        Lcgft(
            "sacred_work",
            "gf2015026045",
            "Sacred works",
            GenreFormMappingKind.Exact),
        Lcgft(
            "sermon",
            "gf2015026051",
            "Sermons",
            GenreFormMappingKind.Exact),
        Lcgft(
            "catechism",
            "gf2015026029",
            "Catechisms",
            GenreFormMappingKind.Exact),
        Lcgft(
            "creed",
            "gf2015026031",
            "Creeds",
            GenreFormMappingKind.Broader),
        Lcgft(
            "devotional_literature",
            "gf2015026057",
            "Devotional literature",
            GenreFormMappingKind.Exact),
        Lcgft(
            "prayer",
            "gf2015026049",
            "Prayers",
            GenreFormMappingKind.Exact),
        Lcgft(
            "biography",
            "gf2014026049",
            "Biographies",
            GenreFormMappingKind.Exact),
        Lcgft(
            "academic_degree_work",
            "gf2014026039",
            "Academic theses",
            GenreFormMappingKind.Broader),
        Lcgft(
            "essays",
            "gf2014026094",
            "Essays",
            GenreFormMappingKind.Exact),
        Lcgft(
            "commentary",
            "gf2025026014",
            "Commentaries",
            GenreFormMappingKind.Exact)
    ];

    #endregion

    #region Methods

    /// <summary>
    /// Gets the approved alignments for one authority.
    /// </summary>
    public static IReadOnlyList<ApologiaGenreFormAuthorityMapping> ForAuthority(
        string authority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authority);

        return Approved
            .Where(x => string.Equals(
                x.Authority,
                authority,
                StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// Derives the stable identity of an alignment.
    /// </summary>
    public static Guid StableId(
        string authority,
        string productTermCode,
        string externalConceptId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authority);
        ArgumentException.ThrowIfNullOrWhiteSpace(productTermCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalConceptId);

        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(
                IdentityNamespace +
                authority +
                "/" +
                productTermCode +
                "/" +
                externalConceptId));

        return new Guid(hash.AsSpan(0, 16));
    }

    #endregion

    #region Methods Helpers

    /// <summary>
    /// Declares one LCGFT alignment, deriving the canonical concept URI from
    /// the authority identifier.
    /// </summary>
    private static ApologiaGenreFormAuthorityMapping Lcgft(
        string productTermCode,
        string externalConceptId,
        string externalConceptLabel,
        GenreFormMappingKind mappingKind) =>
        new(
            productTermCode,
            GenreFormAuthority.Lcgft,
            externalConceptId,
            // The URI scheme is the one the Library of Congress publishes in
            // its bulk dump and therefore the one the local catalogue holds.
            "http://id.loc.gov/authorities/genreForms/" + externalConceptId,
            externalConceptLabel,
            mappingKind);

    #endregion
}
