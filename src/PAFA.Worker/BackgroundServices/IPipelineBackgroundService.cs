using PAFA.Worker.Models;

namespace PAFA.Worker.BackgroundServices;

/// <summary>
/// Exposes the pipeline channel writer so controllers can enqueue jobs
/// without taking a direct dependency on <see cref="PipelineBackgroundService"/>.
/// </summary>
public interface IPipelineBackgroundService
{
    ValueTask EnqueueAsync(PipelineJob job, CancellationToken ct = default);
}
