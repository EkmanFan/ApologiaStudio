using ApologiaStudio.Application.Abstractions.FieldSuggestions;

namespace ApologiaStudio.UnitTests.Application.FieldSuggestions;

/// <summary>
/// The capability when no machine assistance is configured.
/// </summary>
/// <remarks>
/// This is the default state of the product, not a degraded one. It must be
/// callable, answer every field it was asked about, and require no runtime,
/// model or environment variable of any kind.
/// </remarks>
public sealed class UnavailableFieldSuggestionProviderTests
{
    #region Variables and Constants

    private static readonly IFieldSuggestionProvider Provider =
        new UnavailableFieldSuggestionProvider();

    private static readonly FieldSuggestionRequest Request =
        new(
            Guid.NewGuid(),
            new FieldSuggestionEvidence("A Defence of the Faith", null));

    #endregion

    #region Methods

    [Fact]
    public async Task An_explicitly_requested_field_is_answered_unavailable()
    {
        var batch = await Provider.GetSuggestionsAsync(
            Request,
            [ApologiaFieldId.GenreForm],
            CancellationToken.None);

        var result = Assert.Single(batch.Results);

        Assert.Equal(ApologiaFieldId.GenreForm, result.FieldId);
        Assert.Equal(FieldSuggestionStatus.Unavailable, result.Status);
        Assert.Empty(result.Suggestions);
        Assert.Equal(
            UnavailableFieldSuggestionProvider.ProviderId,
            result.Provenance.Provider);
    }

    [Fact]
    public async Task An_unsupported_field_is_answered_rather_than_omitted()
    {
        // Omitting it would read as "nothing to say about this field", which is
        // a different claim from "nothing ran".
        var fields = new[]
        {
            ApologiaFieldId.GenreForm,
            new ApologiaFieldId("language"),
            new ApologiaFieldId("publication_year")
        };

        var batch = await Provider.GetSuggestionsAsync(
            Request,
            fields,
            CancellationToken.None);

        Assert.Equal(3, batch.Results.Count);
        Assert.All(
            batch.Results,
            x => Assert.Equal(FieldSuggestionStatus.Unavailable, x.Status));

        // The requested subset is honoured exactly: no field is invented.
        Assert.Equal(fields, batch.Results.Select(x => x.FieldId));

        batch.EnsureAnswers(fields);
    }

    [Fact]
    public async Task A_field_requested_twice_is_answered_once()
    {
        var batch = await Provider.GetSuggestionsAsync(
            Request,
            [ApologiaFieldId.GenreForm, ApologiaFieldId.GenreForm],
            CancellationToken.None);

        Assert.Single(batch.Results);
    }

    [Fact]
    public async Task No_field_is_evaluated_when_none_is_named()
    {
        // Null means "every field this provider supports". This one supports
        // none, so it answers nothing rather than declaring every conceivable
        // field unavailable.
        var batch = await Provider.GetSuggestionsAsync(
            Request,
            null,
            CancellationToken.None);

        Assert.Empty(batch.Results);
    }

    [Fact]
    public async Task An_empty_field_list_evaluates_nothing()
    {
        var batch = await Provider.GetSuggestionsAsync(
            Request,
            [],
            CancellationToken.None);

        Assert.Empty(batch.Results);
    }

    [Fact]
    public async Task Missing_machine_assistance_is_never_an_exception()
    {
        // Ordinary metadata editing must not depend on a model runtime.
        var batch = await Provider.GetSuggestionsAsync(
            new FieldSuggestionRequest(
                Guid.NewGuid(),
                FieldSuggestionEvidence.Empty),
            [ApologiaFieldId.GenreForm],
            CancellationToken.None);

        Assert.Equal(
            FieldSuggestionStatus.Unavailable,
            Assert.Single(batch.Results).Status);
    }

    [Fact]
    public async Task A_field_without_identity_is_refused()
    {
        await Assert.ThrowsAsync<FieldSuggestionContractException>(
            () => Provider.GetSuggestionsAsync(
                Request,
                [default],
                CancellationToken.None));
    }

    [Fact]
    public async Task A_missing_request_is_refused()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => Provider.GetSuggestionsAsync(
                null!,
                [ApologiaFieldId.GenreForm],
                CancellationToken.None));
    }

    [Fact]
    public async Task Cancellation_stays_cancellation()
    {
        // It must never be reported as Failed: nothing failed.
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Provider.GetSuggestionsAsync(
                Request,
                [ApologiaFieldId.GenreForm],
                cancellation.Token));
    }

    #endregion
}
