using System.Collections.Concurrent;
using PAFA.Domain.Enums;
using PAFA.Worker.Models;

namespace PAFA.Worker.State;

/// <summary>
/// Thread-safe, in-memory implementation of <see cref="IPipelineStateStore"/>.
/// Uses a <see cref="ConcurrentDictionary{TKey,TValue}"/> — no external dependency.
/// </summary>
public sealed class InMemoryPipelineStateStore : IPipelineStateStore
{
    private readonly ConcurrentDictionary<Guid, PipelineExecutionState> _store = new();

    /// <inheritdoc/>
    public void Set(Guid jobId, PipelineExecutionState state)
        => _store[jobId] = state;

    /// <inheritdoc/>
    public PipelineExecutionState? Get(Guid jobId)
        => _store.TryGetValue(jobId, out var state) ? state : null;

    /// <inheritdoc/>
    public bool IsRunningForMonth(int year, int month)
        => _store.Values.Any(s =>
            s.Year          == year
         && s.Month         == month
         && s.OverallStatus == PipelineStatus.Running);
}
