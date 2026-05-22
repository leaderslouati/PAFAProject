using PAFA.Domain.Enums;

namespace PAFA.Worker.Models;

/// <summary>
/// In-memory state of a single pipeline execution, held in <see cref="IPipelineStateStore"/>.
/// Updated after each step and pushed to connected SignalR clients.
/// </summary>
public sealed class PipelineExecutionState
{
    public Guid JobId         { get; set; }
    public Guid CorrelationId { get; set; }
    public int  Year          { get; set; }
    public int  Month         { get; set; }

    public DateTime  StartedAt  { get; set; }
    public DateTime? FinishedAt { get; set; }

    public PipelineStatus OverallStatus { get; set; } = PipelineStatus.Pending;

    public List<PipelineStepState> Steps { get; set; } = [];
}

/// <summary>State of a single step within a <see cref="PipelineExecutionState"/>.</summary>
public sealed class PipelineStepState
{
    public int        StepNumber { get; set; }
    public string     Name       { get; set; } = string.Empty;
    public StepStatus Status     { get; set; } = StepStatus.Pending;
    public long?      DurationMs { get; set; }
    public string?    Error      { get; set; }

    /// <summary>
    /// Typed step result serialised as-is for the frontend
    /// (ImportedFile list, ParseAndValidateResult list, or PersistenceReport).
    /// </summary>
    public object? Result { get; set; }
}
