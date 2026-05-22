namespace PAFA.Worker.Models;

/// <summary>
/// Represents a single pipeline execution request queued in the background service.
/// </summary>
public sealed record PipelineJob(
    Guid JobId,
    int  Year,
    int  Month);
