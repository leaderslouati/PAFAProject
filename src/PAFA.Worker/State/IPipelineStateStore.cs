using PAFA.Worker.Models;

namespace PAFA.Worker.State;

/// <summary>
/// In-memory store for active and recently completed pipeline executions.
/// Backed by a <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/>.
/// State is lost on application restart — no persistence required.
/// </summary>
public interface IPipelineStateStore
{
    /// <summary>Creates or replaces the state for the given job.</summary>
    void Set(Guid jobId, PipelineExecutionState state);

    /// <summary>Returns the state for <paramref name="jobId"/>, or null if unknown.</summary>
    PipelineExecutionState? Get(Guid jobId);

    /// <summary>
    /// Returns true when a pipeline with status Running already exists for
    /// the given year/month combination, preventing duplicate executions.
    /// </summary>
    bool IsRunningForMonth(int year, int month);
}
