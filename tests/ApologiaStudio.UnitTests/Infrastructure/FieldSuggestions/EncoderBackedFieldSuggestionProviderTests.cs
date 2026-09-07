using ApologiaStudio.Application.Abstractions.FieldSuggestions;
using ApologiaStudio.Infrastructure.Knowledge.FieldSuggestions;

namespace ApologiaStudio.UnitTests.Infrastructure.FieldSuggestions;

/// <summary>
/// Field selection on the encoder-backed capability.
/// </summary>
/// <remarks>
/// The provider answers Genre/Form through the frozen plan and every other
/// field as unavailable. What matters here is that no requested field is ever
/// dropped, and that a field the provider cannot serve is said so rather than
/// omitted.
/// </remarks>
public sealed class EncoderBackedFieldSuggestionProviderTests
{
    #region Variables and Constants

    private static readonly FieldSuggestionRequest Request =
        new(
            Guid.NewGuid(),
            new FieldSuggestionEvidence("A Defence of the Faith", null));

    #endregion

    #region Methods

    [Fact]
    public async Task Genre_form_is_answered_by_the_plan()
    {
        var batch = await Provider().GetSuggestionsAsync(
            Request,
            [ApologiaFieldId.GenreForm],
            CancellationToken.None);

        var result = Assert.Single(batch.Results);

        Assert.Equal(ApologiaFieldId.GenreForm, result.FieldId);
        Assert.Equal(FieldSuggestionStatus.Succeeded, result.Status);
        Assert.Equal(
            "apologetic_writing",
            Assert.Single(result.Suggestions).ValueId);
        Assert.Equal(
            EncoderBackedFieldSuggestionProvider.ProviderId,
            result.Provenance.Provider);
    }

    [Fact]
    public async Task Genre_form_is_the_only_field_evaluated_by_default()
    {
        // Null means "every field this provider supports".
        var batch = await Provider().GetSuggestionsAsync(
            Request,
            null,
            CancellationToken.None);

        Assert.Equal(
            ApologiaFieldId.GenreForm,
            Assert.Single(batch.Results).FieldId);
    }

    [Fact]
    public async Task An_unsupported_field_is_answered_rather_than_omitted()
    {
        var fields = new[]
        {
            new ApologiaFieldId("language"),
            ApologiaFieldId.GenreForm,
            new ApologiaFieldId("publication_year")
        };

        var batch = await Provider().GetSuggestionsAsync(
            Request,
            fields,
            CancellationToken.None);

        Assert.Equal(3, batch.Results.Count);
        Assert.Equal(fields, batch.Results.Select(x => x.FieldId));
        batch.EnsureAnswers(fields);

        Assert.Equal(
            FieldSuggestionStatus.Unavailable,
            batch.Find(new ApologiaFieldId("language"))!.Status);
        Assert.Equal(
            FieldSuggestionStatus.Succeeded,
            batch.Find(ApologiaFieldId.GenreForm)!.Status);
    }

    [Fact]
    public async Task A_field_requested_twice_is_answered_once()
    {
        var batch = await Provider().GetSuggestionsAsync(
            Request,
            [ApologiaFieldId.GenreForm, ApologiaFieldId.GenreForm],
            CancellationToken.None);

        Assert.Single(batch.Results);
    }

    [Fact]
    public async Task Cancellation_stays_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Provider().GetSuggestionsAsync(
                Request,
                [ApologiaFieldId.GenreForm],
                cancellation.Token));
    }

    [Fact]
    public async Task A_missing_request_is_refused()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => Provider().GetSuggestionsAsync(
                null!,
                [ApologiaFieldId.GenreForm],
                CancellationToken.None));
    }

    #endregion

    #region Methods Helpers

    private static IFieldSuggestionProvider Provider() =>
        new EncoderBackedFieldSuggestionProvider(
            new GenreFormInferencePlan(new SingleLabelRuntime()));

    /// <summary>
    /// Answers a confident apologetic_writing on the primary, so field
    /// selection is what these tests actually observe.
    /// </summary>
    private sealed class SingleLabelRuntime : IEncoderInferenceRuntime
    {
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        }

        public Task<EncoderInferenceResult> InferAsync(
            EncoderInferenceRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scores = Enumerable
                .Repeat(0d, GenreFormEncoderLabelMap.OutputCount)
                .ToList();

            scores[GenreFormEncoderLabelMap.OutputOrder
                .Select((code, index) => (code, index))
                .First(x => x.code == "apologetic_writing")
                .index] = 0.93;

            return Task.FromResult(
                new EncoderInferenceResult(
                    request.ModelId,
                    "sha256:test",
                    scores,
                    9.0));
        }
    }

    #endregion
}
