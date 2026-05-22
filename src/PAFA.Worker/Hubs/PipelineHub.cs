using Microsoft.AspNetCore.SignalR;

namespace PAFA.Worker.Hubs;

/// <summary>
/// SignalR hub for real-time pipeline step notifications.
///
/// Clients call <see cref="JoinJob"/> to subscribe to updates for a specific job.
/// The server emits "StepUpdated" events to the job's group after each pipeline step.
///
/// Route: /hubs/pipeline
/// </summary>
public sealed class PipelineHub : Hub
{
    private readonly ILogger<PipelineHub> _log;

    public PipelineHub(ILogger<PipelineHub> log) => _log = log;

    /// <summary>
    /// Subscribes the current connection to the SignalR group identified by <paramref name="jobId"/>.
    /// Must be called by the frontend immediately after receiving the 202 response from
    /// POST /api/pipeline/run.
    /// </summary>
    public async Task JoinJob(string jobId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, jobId);
        _log.LogInformation(
            "[PIPELINE_HUB] Client {ConnectionId} joined job group {JobId}",
            Context.ConnectionId, jobId);
    }
}
