using ApologiaStudio.Application.Abstractions.FieldSuggestions;
using ApologiaStudio.Application.Knowledge.GenreForms;
using ApologiaStudio.Infrastructure.Knowledge.FieldSuggestions;

namespace ApologiaStudio.UnitTests.Infrastructure.FieldSuggestions;

/// <summary>
/// The frozen contract between Apologia and the Spike Encoder V2.1 artifacts.
/// </summary>
/// <remarks>
/// Serialization and label order are properties of two trained models. A change
/// here is not a refactor, it is a different input distribution and a different
/// meaning for every output index.
/// </remarks>
public sealed class GenreFormEncoderContractTests
{
    #region Methods Serialization

    [Fact]
    public void The_two_recorded_training_shapes_are_reproduced()
    {
        // The models saw exactly two shapes: [TITLE] alone (3 210 records) and
        // [TITLE] then [DESCRIPTION] (2 696). Real recorded outputs are
        // asserted byte for byte by SpikeSerializerGoldenTests.
        Assert.Equal(
            "[TITLE] A Defence of the Faith",
            GenreFormEncoderSerializer.Serialize(
                new FieldSuggestionEvidence("A Defence of the Faith", null)));

        Assert.Equal(
            "[TITLE] A Defence of the Faith\n[DESCRIPTION] Collected essays.",
            GenreFormEncoderSerializer.Serialize(
                new FieldSuggestionEvidence(
                    "A Defence of the Faith",
                    "Collected essays.")));
    }

    [Fact]
    public void An_absent_section_emits_nothing_at_all()
    {
        // No empty marker was ever emitted in training, and [SUBTITLE], [TOC]
        // and [STRUCTURE] never appeared in a single record.
        var serialized = GenreFormEncoderSerializer.Serialize(
            new FieldSuggestionEvidence("A Defence of the Faith", null));

        Assert.Equal("[TITLE] A Defence of the Faith", serialized);
        Assert.DoesNotContain("[SUBTITLE]", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("[DESCRIPTION]", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("[TOC]", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("[STRUCTURE]", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void A_description_without_a_title_still_emits_only_what_exists()
    {
        Assert.Equal(
            "[DESCRIPTION] Collected sermons.",
            GenreFormEncoderSerializer.Serialize(
                new FieldSuggestionEvidence(null, "Collected sermons.")));
    }

    [Fact]
    public void No_evidence_serializes_to_nothing()
    {
        Assert.Equal(
            string.Empty,
            GenreFormEncoderSerializer.Serialize(FieldSuggestionEvidence.Empty));
        Assert.Equal(
            string.Empty,
            GenreFormEncoderSerializer.Serialize(
                new FieldSuggestionEvidence("   ", "\t")));
    }

    [Fact]
    public void Values_pass_through_unmodified()
    {
        // The Spike applied no normalisation: 23 training values carry double
        // spaces and 6 carry trailing whitespace.
        Assert.Equal(
            "[TITLE] Two  spaces and a trailing one \n[DESCRIPTION]  leading",
            GenreFormEncoderSerializer.Serialize(
                new FieldSuggestionEvidence(
                    "Two  spaces and a trailing one ",
                    " leading")));
    }

    #endregion

    #region Methods Label Map

    [Fact]
    public void The_output_order_is_the_frozen_artifact_order()
    {
        Assert.Equal(24, GenreFormEncoderLabelMap.OutputCount);
        Assert.Equal(
            [
                "textbook", "handbook_manual", "dictionary", "encyclopedia",
                "academic_degree_work", "conference_proceedings", "anthology",
                "collected_works", "edited_volume", "biography", "autobiography",
                "personal_narrative", "essays", "commentary",
                "apologetic_writing", "catechism", "creed",
                "devotional_literature", "prayer", "sacred_work", "sermon",
                "scholarly_article", "correspondence", "diary"
            ],
            GenreFormEncoderLabelMap.OutputOrder);
    }

    [Fact]
    public void Every_output_is_an_encoder_predictable_product_term()
    {
        var predictable = ApologiaGenreFormTaxonomy.EncoderPredictableTerms
            .Select(x => x.Code)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(24, predictable.Count);
        Assert.All(
            GenreFormEncoderLabelMap.OutputOrder,
            code => Assert.Contains(code, predictable));
        Assert.Empty(
            predictable.Except(
                GenreFormEncoderLabelMap.OutputOrder,
                StringComparer.Ordinal));
    }

    [Fact]
    public void No_manual_only_term_can_be_an_encoder_output()
    {
        // The artifacts have no head for these, and no index may ever map to
        // one: a machine must not propose a term reserved to a reviewer.
        foreach (var code in ApologiaGenreFormTaxonomy.ManualOnlyTerms
                     .Select(x => x.Code))
        {
            Assert.DoesNotContain(code, GenreFormEncoderLabelMap.OutputOrder);
        }
    }

    [Fact]
    public void An_index_outside_the_declared_outputs_fails_closed()
    {
        Assert.Throws<EncoderInferenceException>(
            () => GenreFormEncoderLabelMap.CodeAt(-1));
        Assert.Throws<EncoderInferenceException>(
            () => GenreFormEncoderLabelMap.CodeAt(24));

        Assert.Equal("textbook", GenreFormEncoderLabelMap.CodeAt(0));
        Assert.Equal("diary", GenreFormEncoderLabelMap.CodeAt(23));
    }

    #endregion
}
