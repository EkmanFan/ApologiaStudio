using ApologiaStudio.Web.FieldSuggestions;

namespace ApologiaStudio.UnitTests.Web.FieldSuggestions;

/// <summary>
/// The queue that carries a new draft to its automatic analysis.
/// </summary>
/// <remarks>
/// Its whole job is to be cheap and to forget nothing twice. Enqueuing must
/// stay a synchronous write so the ingestion pipeline never waits, and the same
/// draft must not be analysed twice because a result was replayed.
/// </remarks>
public sealed class GenreFormAnalysisQueueTests
{
    #region Methods

    [Fact]
    public async Task A_queued_draft_is_delivered_once()
    {
        var queue = new GenreFormAnalysisQueue();
        var draftId = Guid.NewGuid();

        Assert.True(queue.TryEnqueue(draftId));

        using var cancellation = new CancellationTokenSource(
            TimeSpan.FromSeconds(5));

        await foreach (var delivered in queue.ReadAllAsync(cancellation.Token))
        {
            Assert.Equal(draftId, delivered);
            break;
        }
    }

    [Fact]
    public void The_same_draft_is_never_queued_twice_at_once()
    {
        // A replayed Manager result must not produce a second analysis.
        var queue = new GenreFormAnalysisQueue();
        var draftId = Guid.NewGuid();

        Assert.True(queue.TryEnqueue(draftId));
        Assert.False(queue.TryEnqueue(draftId));

        // Once the analysis is done, the draft may be queued again if ever
        // needed.
        queue.Release(draftId);

        Assert.True(queue.TryEnqueue(draftId));
    }

    [Fact]
    public void An_empty_identity_is_refused()
    {
        Assert.False(new GenreFormAnalysisQueue().TryEnqueue(Guid.Empty));
    }

    [Fact]
    public void A_burst_is_bounded_rather_than_unbounded()
    {
        // Ingesting a corpus must not grow memory. A dropped automatic
        // analysis costs nothing: the reviewer's button still runs one.
        var queue = new GenreFormAnalysisQueue();

        var accepted = Enumerable
            .Range(0, 1_000)
            .Count(_ => queue.TryEnqueue(Guid.NewGuid()));

        Assert.InRange(accepted, 1, 512);
    }

    [Fact]
    public void Enqueuing_does_not_block()
    {
        // The ingestion pipeline calls this inline; it must be a write and
        // nothing else.
        var queue = new GenreFormAnalysisQueue();
        var started = DateTimeOffset.UtcNow;

        for (var index = 0; index < 200; index++)
        {
            queue.TryEnqueue(Guid.NewGuid());
        }

        Assert.True(DateTimeOffset.UtcNow - started < TimeSpan.FromSeconds(1));
    }

    #endregion
}
