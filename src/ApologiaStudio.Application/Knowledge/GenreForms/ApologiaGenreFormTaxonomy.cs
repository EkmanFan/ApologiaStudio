using System.Security.Cryptography;
using System.Text;

namespace ApologiaStudio.Application.Knowledge.GenreForms;

/// <summary>
/// Whether the machine classifier may propose a term.
/// </summary>
public enum GenreFormPredictionMode
{
    /// <summary>The classifier may propose this term.</summary>
    EncoderPredictable = 0,

    /// <summary>Only a reviewer may assign this term.</summary>
    ManualOnly = 1
}

/// <summary>
/// Whether a term is part of the active vocabulary.
/// </summary>
public enum GenreFormTermStatus
{
    /// <summary>Available for classification and review.</summary>
    Active = 0,

    /// <summary>Kept for historical assignments; never proposed or selectable.</summary>
    Retired = 1
}

/// <summary>
/// One canonical Apologia Genre/Form term.
/// </summary>
/// <remarks>
/// Identity is <see cref="Code"/>. A display label, an LCGFT URI, a BnF
/// identifier or an encoder head index are never identity: labels are
/// translated, external authorities are alignment, and head indices belong to
/// one trained model.
/// </remarks>
public sealed record ApologiaGenreFormTerm(
    string Code,
    string PreferredLabel,
    string Definition,
    GenreFormPredictionMode PredictionMode,
    int DisplayOrder)
{
    /// <summary>
    /// Gets the stable product identity of the concept.
    /// </summary>
    public Guid Id =>
        ApologiaGenreFormTaxonomy.StableId(
            Code);
}

/// <summary>
/// The canonical Apologia Genre/Form product taxonomy, V1.
/// </summary>
/// <remarks>
/// Product-owned reference data. External bibliographic vocabularies are
/// alignment and provenance sources; they do not define what an Apologia
/// Genre/Form concept is.
///
/// The taxonomy is deliberately flat in V1: no term declares a broader or
/// narrower product term. The LCGFT hierarchy stays in the external authority
/// catalogue, where it describes that authority rather than this product.
///
/// Definitions come verbatim from the Spike Encoder V2 labelling policy, the
/// authoritative product source, and are therefore mostly French. Only
/// <c>creed</c> carries a different wording, fixed in English by the V2.1
/// decision that broadened it beyond religion.
/// </remarks>
public static class ApologiaGenreFormTaxonomy
{
    #region Variables and Constants

    /// <summary>
    /// Stable identifier of the V1 taxonomy.
    /// </summary>
    public const string Version =
        "apologia-genre-form-v1";

    /// <summary>
    /// Namespace for deriving a term's stable identity.
    /// </summary>
    /// <remarks>
    /// It deliberately carries no taxonomy version. A concept whose meaning does
    /// not change must keep the same identity across taxonomy versions, so that
    /// an assignment made under V1 still points at the same concept under V2.
    /// The version is an attribute of the term, never part of its identity.
    /// </remarks>
    private const string IdentityNamespace =
        "apologia-genre-form-term/";

    /// <summary>
    /// The 27 canonical V1 terms, in product display order.
    /// </summary>
    public static IReadOnlyList<ApologiaGenreFormTerm> Terms { get; } =
    [
        new(
            "textbook",
            "Textbook",
            "Ressource conçue principalement pour l'enseignement ou l'apprentissage structuré d'une discipline ou d'un domaine, notamment dans un contexte scolaire, universitaire ou de formation.",
            GenreFormPredictionMode.EncoderPredictable,
            1),
        new(
            "handbook_manual",
            "Handbook or manual",
            "Ressource de consultation ou d'application pratique fournissant procédures, règles, méthodes, instructions ou références opérationnelles.",
            GenreFormPredictionMode.EncoderPredictable,
            2),
        new(
            "dictionary",
            "Dictionary",
            "Ressource structurée principalement comme ensemble d'entrées lexicales ou terminologiques donnant définitions, équivalents, explications ou informations associées.",
            GenreFormPredictionMode.EncoderPredictable,
            3),
        new(
            "encyclopedia",
            "Encyclopedia",
            "Ressource de référence organisée en articles ou entrées visant une couverture synthétique d'un domaine ou de connaissances générales.",
            GenreFormPredictionMode.EncoderPredictable,
            4),
        new(
            "academic_degree_work",
            "Degree-qualifying academic work",
            "Travail académique substantiel produit dans le cadre formel de l'obtention d'un diplôme, grade ou certification académique/professionnelle.",
            GenreFormPredictionMode.EncoderPredictable,
            5),
        new(
            "conference_proceedings",
            "Conference papers and proceedings",
            "Publication réunissant les communications, articles, résumés ou comptes rendus issus d'une conférence, colloque, congrès, symposium ou workshop.",
            GenreFormPredictionMode.EncoderPredictable,
            6),
        new(
            "anthology",
            "Anthology",
            "Sélection intentionnelle de textes, œuvres ou contributions distinctes, réunis pour représenter un thème, une période, un genre, une tradition ou un corpus. Le caractère structurant est : ```text selection + curation",
            GenreFormPredictionMode.EncoderPredictable,
            7),
        new(
            "collected_works",
            "Collected works",
            "Réunion de plusieurs œuvres distinctes d'un même auteur, généralement avec une visée d'exhaustivité, de sélection représentative ou de conservation de son corpus. Le caractère structurant est : ```text same author",
            GenreFormPredictionMode.EncoderPredictable,
            8),
        new(
            "edited_volume",
            "Edited volume",
            "Ouvrage composé de contributions autonomes de plusieurs auteurs, coordonnées par un ou plusieurs directeurs scientifiques ou éditeurs autour d'un thème commun. Le caractère structurant est : ```text coordinated contributions",
            GenreFormPredictionMode.EncoderPredictable,
            9),
        new(
            "biography",
            "Biography",
            "Récit substantiel de la vie d'une personne écrit principalement par une autre personne.",
            GenreFormPredictionMode.EncoderPredictable,
            10),
        new(
            "autobiography",
            "Autobiography",
            "Œuvre dans laquelle une personne raconte rétrospectivement sa propre vie, ou une part substantielle de celle-ci, avec sa trajectoire personnelle comme objet principal. Principe discriminant : ```text \"I tell the story of my life\"",
            GenreFormPredictionMode.EncoderPredictable,
            11),
        new(
            "personal_narrative",
            "Personal narrative",
            "Récit non fictionnel dans lequel l'auteur témoigne principalement d'événements, d'une période, d'une expérience ou d'un contexte qu'il a personnellement vécus. Principe discriminant : ```text \"I tell what I lived through\"",
            GenreFormPredictionMode.EncoderPredictable,
            12),
        new(
            "essays",
            "Essays",
            "Œuvre non fictionnelle relativement brève, ou ensemble de telles œuvres, principalement réflexive, interprétative ou argumentative, dans laquelle un auteur développe un point de vue personnel ou intellectuel sans viser l'exposition systématique caractéristique d'un traité, d'un manuel ou d'une monographie de recherche.",
            GenreFormPredictionMode.EncoderPredictable,
            13),
        new(
            "commentary",
            "Commentary",
            "Œuvre structurée principalement autour de l'explication, de l'interprétation ou de l'analyse suivie d'une autre œuvre ou d'un texte source identifiable.",
            GenreFormPredictionMode.EncoderPredictable,
            14),
        new(
            "apologetic_writing",
            "Apologetic writing",
            "Œuvre dont l'objectif principal est de défendre, justifier ou expliquer rationnellement une religion, confession ou système religieux face à des objections, critiques ou alternatives.",
            GenreFormPredictionMode.EncoderPredictable,
            15),
        new(
            "catechism",
            "Catechism",
            "Exposé systématique de doctrine religieuse destiné à l'instruction, historiquement souvent organisé en questions/réponses ou sous une forme pédagogique équivalente.",
            GenreFormPredictionMode.EncoderPredictable,
            16),
        new(
            "creed",
            "Creed",
            "A formal or explicit profession of beliefs, principles, convictions, or commitments, religious or non-religious. A work about a creed is not necessarily itself a creed.",
            GenreFormPredictionMode.EncoderPredictable,
            17),
        new(
            "devotional_literature",
            "Devotional literature",
            "Œuvre principalement destinée à nourrir la pratique dévotionnelle, la méditation, la piété ou la vie spirituelle personnelle/communautaire.",
            GenreFormPredictionMode.EncoderPredictable,
            18),
        new(
            "prayer",
            "Prayer",
            "Ressource constituée principalement de textes de prière ou d'une prière autonome cataloguée comme œuvre.",
            GenreFormPredictionMode.EncoderPredictable,
            19),
        new(
            "sacred_work",
            "Sacred work",
            "Texte considéré comme écriture sacrée/canonique dans une tradition religieuse et publié comme ce texte ou une édition/traduction de celui-ci.",
            GenreFormPredictionMode.EncoderPredictable,
            20),
        new(
            "sermon",
            "Sermon",
            "Texte ou recueil de discours religieux conçus à l'origine comme prédications ou homélies adressées à une assemblée.",
            GenreFormPredictionMode.EncoderPredictable,
            21),
        new(
            "scholarly_article",
            "Scholarly article",
            "Article autonome à visée académique ou scientifique, publié ou destiné à être publié dans un contexte savant identifié.",
            GenreFormPredictionMode.EncoderPredictable,
            22),
        new(
            "correspondence",
            "Correspondence",
            "Lettre individuelle ou ensemble de lettres/échanges écrits dont la fonction documentaire principale est la correspondance entre personnes ou institutions.",
            GenreFormPredictionMode.EncoderPredictable,
            23),
        new(
            "diary",
            "Diary",
            "Ressource constituée d'entrées personnelles consignées au fil du temps, généralement proches temporellement des événements vécus. Principe discriminant : ```text written as events unfold",
            GenreFormPredictionMode.EncoderPredictable,
            24),
        new(
            "study_guide",
            "Study guide",
            "Ressource explicitement conçue pour guider l'étude, la révision ou l'apprentissage d'une œuvre, d'un cours ou d'un sujet.",
            GenreFormPredictionMode.ManualOnly,
            25),
        new(
            "training_material",
            "Training material",
            "Ressource conçue principalement pour accompagner une action de formation structurée, généralement comme support d'un cours, atelier, programme ou dispositif de formation.",
            GenreFormPredictionMode.ManualOnly,
            26),
        new(
            "instructional_lesson",
            "Lecture / lesson",
            "Contenu pédagogique conçu principalement pour être dispensé à des apprenants comme unité d'enseignement, indépendamment de son support matériel. Le support ne définit pas le concept : ```text PDF PPTX video audio transcript ``` peuvent réaliser le même contenu pédagogique.",
            GenreFormPredictionMode.ManualOnly,
            27),
    ];

    /// <summary>
    /// Gets the terms the machine classifier may propose.
    /// </summary>
    public static IEnumerable<ApologiaGenreFormTerm> EncoderPredictableTerms =>
        Terms.Where(
            term =>
                term.PredictionMode == GenreFormPredictionMode.EncoderPredictable);

    /// <summary>
    /// Gets the terms only a reviewer may assign.
    /// </summary>
    public static IEnumerable<ApologiaGenreFormTerm> ManualOnlyTerms =>
        Terms.Where(
            term =>
                term.PredictionMode == GenreFormPredictionMode.ManualOnly);

    #endregion

    #region Methods

    /// <summary>
    /// Derives the stable product identity of a term code.
    /// </summary>
    /// <remarks>
    /// Deterministic, so the same concept carries the same identity in every
    /// environment without a lookup, and a re-seed cannot mint a second row for
    /// a term that already exists.
    /// </remarks>
    /// <param name="code">Canonical term code.</param>
    /// <returns>The term's stable identity.</returns>
    public static Guid StableId(
        string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(
                IdentityNamespace + code));

        return new Guid(hash.AsSpan(0, 16));
    }

    /// <summary>
    /// Finds a term by its canonical code.
    /// </summary>
    public static ApologiaGenreFormTerm? Find(
        string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : Terms.FirstOrDefault(
                term =>
                    string.Equals(
                        term.Code,
                        code.Trim(),
                        StringComparison.Ordinal));

    #endregion
}
