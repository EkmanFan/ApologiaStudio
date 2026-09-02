using System.Threading.Channels;

namespace ApologiaStudio.Web.DocumentManager;

public sealed class DocumentManagerConsumptionSignal
{
    private readonly Channel<bool> _signals =
        Channel.CreateBounded<bool>(
            new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false
            });

    public void Notify() => _signals.Writer.TryWrite(true);

    public async ValueTask WaitAsync(CancellationToken cancellationToken) =>
        _ = await _signals.Reader.ReadAsync(cancellationToken)
            .ConfigureAwait(false);
}
