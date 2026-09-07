using System.Text;
using ApologiaStudio.Application.Abstractions.FieldSuggestions;
using ApologiaStudio.Application.Knowledge.GenreForms;

namespace ApologiaStudio.Infrastructure.Knowledge.FieldSuggestions;

/// <summary>
/// The serialized input the Spike Encoder V2.1 models were trained on.
/// </summary>
/// <remarks>
/// Reproduces the authoritative Spike serializer exactly: a present section is
/// emitted as its marker, a space and its raw value on one line; sections are
/// joined by a newline; an absent section emits nothing at all.
///
/// The V2.1 handoff names five canonical sections. The 5 906 records the models
/// actually saw contain only two shapes — <c>[TITLE]</c> alone (3 210) and
/// <c>[TITLE]</c> with <c>[DESCRIPTION]</c> (2 696). <c>[SUBTITLE]</c>,
/// <c>[TOC]</c> and <c>[STRUCTURE]</c> never appeared, and no empty section was
/// ever emitted. Emitting empty markers would therefore feed the models a shape
/// they have never seen, which is why the code rules win over the prose.
///
/// Values pass through unmodified — the Spike applied no normalisation, and 23
/// training values contain double spaces, 6 trailing whitespace. The one
/// deliberate difference is that a whitespace-only value counts as absent,
/// which the harvester's own truthiness test achieved for empty strings.
/// </remarks>
public static class GenreFormEncoderSerializer
{
    #region Methods

    /// <summary>
    /// Serializes the evidence for the encoder.
    /// </summary>
    /// <returns>
    /// The serialized input, or an empty string when no section is available.
    /// </returns>
    public static string Serialize(
        FieldSuggestionEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var builder = new StringBuilder();

        Append(builder, "TITLE", evidence.Title);
        Append(builder, "DESCRIPTION", evidence.Description);

        return builder.ToString();
    }

    private static void Append(
        StringBuilder builder,
        string marker,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append('\n');
        }

        builder.Append('[').Append(marker).Append("] ").Append(value);
    }

    #endregion
}

/// <summary>
/// The 24 encoder outputs, in the models' own order.
/// </summary>
/// <remarks>
/// Declared here rather than derived from the taxonomy, because this order is a
/// property of two frozen artifacts: it must not silently follow a later
/// product reordering. The relation to the taxonomy is asserted instead, and a
/// divergence fails at type initialisation rather than at inference.
///
/// Mapping is by product code only. A preferred label is a translation surface
/// and an LCGFT identity belongs to an external catalogue; neither is ever an
/// encoder output identity.
/// </remarks>
public static class GenreFormEncoderLabelMap
{
    #region Variables and Constants

    /// <summary>
    /// The authoritative output order, taken from the artifacts' own
    /// <c>id2label</c>.
    /// </summary>
    public static IReadOnlyList<string> OutputOrder { get; } =
    [
        "textbook",
        "handbook_manual",
        "dictionary",
        "encyclopedia",
        "academic_degree_work",
        "conference_proceedings",
        "anthology",
        "collected_works",
        "edited_volume",
        "biography",
        "autobiography",
        "personal_narrative",
        "essays",
        "commentary",
        "apologetic_writing",
        "catechism",
        "creed",
        "devotional_literature",
        "prayer",
        "sacred_work",
        "sermon",
        "scholarly_article",
        "correspondence",
        "diary"
    ];

    /// <summary>
    /// The number of outputs the artifacts declare.
    /// </summary>
    public const int OutputCount = 24;

    #endregion

    #region Constructors

    static GenreFormEncoderLabelMap()
    {
        if (OutputOrder.Count != OutputCount)
        {
            throw new InvalidOperationException(
                $"The encoder output order declares {OutputOrder.Count} " +
                $"labels but {OutputCount} are expected.");
        }

        if (OutputOrder.Distinct(StringComparer.Ordinal).Count() != OutputCount)
        {
            throw new InvalidOperationException(
                "The encoder output order contains a duplicate code.");
        }

        var predictable = ApologiaGenreFormTaxonomy.EncoderPredictableTerms
            .Select(x => x.Code)
            .ToHashSet(StringComparer.Ordinal);

        var unknown = OutputOrder.FirstOrDefault(x => !predictable.Contains(x));

        if (unknown is not null)
        {
            throw new InvalidOperationException(
                $"Encoder output '{unknown}' is not an encoder-predictable " +
                "Apologia product term.");
        }

        var unmapped = predictable
            .Except(OutputOrder, StringComparer.Ordinal)
            .FirstOrDefault();

        if (unmapped is not null)
        {
            throw new InvalidOperationException(
                $"Encoder-predictable term '{unmapped}' has no encoder " +
                "output.");
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets the product code for one output index.
    /// </summary>
    public static string CodeAt(int index)
    {
        if (index < 0 || index >= OutputCount)
        {
            throw new EncoderInferenceException(
                $"Output index {index} is outside the {OutputCount} declared " +
                "encoder outputs.");
        }

        return OutputOrder[index];
    }

    #endregion
}
