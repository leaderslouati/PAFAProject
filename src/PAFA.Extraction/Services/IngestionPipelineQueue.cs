using System.Threading.Channels;

namespace PAFA.Extraction.Services;

/// <summary>
/// Implémentation de <see cref="IIngestionPipelineQueue"/> basée sur
/// <see cref="System.Threading.Channels.Channel{T}"/> (bounded, backpressure).
///
/// Capacité maximale : 200 messages en attente simultanément.
/// Si la capacité est dépassée, <see cref="EnqueueAsync"/> attend qu'une place
/// se libère plutôt que de rejeter le message.
/// </summary>
public sealed class IngestionPipelineQueue : IIngestionPipelineQueue
{
    private readonly Channel<PipelineFileMessage> _channel;

    public IngestionPipelineQueue()
    {
        _channel = Channel.CreateBounded<PipelineFileMessage>(new BoundedChannelOptions(200)
        {
            FullMode      = BoundedChannelFullMode.Wait,
            SingleReader  = true,
            SingleWriter  = false
        });
    }

    /// <inheritdoc/>
    public async ValueTask EnqueueAsync(PipelineFileMessage message, CancellationToken ct = default)
        => await _channel.Writer.WriteAsync(message, ct);

    /// <inheritdoc/>
    public async ValueTask<PipelineFileMessage> DequeueAsync(CancellationToken ct = default)
        => await _channel.Reader.ReadAsync(ct);
}
