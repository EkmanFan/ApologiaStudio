using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ApologiaStudio.Web.FieldSuggestions;

/// <summary>
/// Drafts waiting for their automatic Genre/Form analysis.
/// </summary>
/// <remarks>
/// The same shape as <c>DocumentManagerConsumptionSignal</c>: a bounded channel
/// and nothing else. A broker, a durable queue or a generic event bus would all
/// be larger than the problem, which is "run one best-effort analysis soon
/// after a draft appears".
///
/// Bounded and dropping on overflow on purpose. A burst of ingested documents
/// must never grow memory, and a missed automatic analysis costs nothing: the
/// reviewer's button still runs one.
/// </remarks>
public sealed class GenreFormAnalysisQueue
{
    #region Variables and Constants

    private const int Capacity = 256;

    private readonly Channel<Guid> _drafts =
        Channel.CreateBounded<Guid>(
            new BoundedChannelOptions(Capacity)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false
            });

    private readonly ConcurrentDictionary<Guid, byte> _pending = new();

    #endregion

    #region Methods

    /// <summary>
    /// Requests an analysis for a draft, unless one is already queued for it.
    /// </summary>
    public bool TryEnqueue(Guid draftId)
    {
        if (draftId == Guid.Empty)
        {
            return false;
        }

        // The channel drops on overflow and still reports success, so the
        // bound has to be enforced here as well. Without it the pending set
        // would grow without limit and would permanently refuse to re-queue
        // drafts whose request was silently thrown away.
        if (_pending.Count >= Capacity)
        {
            return false;
        }

        if (!_pending.TryAdd(draftId, 0))
        {
            return false;
        }

        if (_drafts.Writer.TryWrite(draftId))
        {
            return true;
        }

        _pending.TryRemove(draftId, out _);

        return false;
    }

    public IAsyncEnumerable<Guid> ReadAllAsync(
        CancellationToken cancellationToken) =>
        _drafts.Reader.ReadAllAsync(cancellationToken);

    /// <summary>
    /// Marks a draft as no longer queued, so a later document can be analysed
    /// again if it ever needs to be.
    /// </summary>
    public void Release(Guid draftId) => _pending.TryRemove(draftId, out _);

    #endregion
}
