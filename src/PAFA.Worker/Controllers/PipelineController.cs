using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PAFA.Worker.BackgroundServices;
using PAFA.Worker.Models;
using PAFA.Worker.State;

namespace PAFA.Worker.Controllers;

/// <summary>
/// Endpoints for manually triggering and monitoring the three-step ingestion pipeline.
///
/// POST /api/pipeline/run
///   Validates the year/month, checks for an already-running job for that period,
///   enqueues the job in the background service, and returns 202 Accepted immediately.
///
/// GET /api/pipeline/status/{jobId}
///   Returns the full <see cref="PipelineExecutionState"/> for a known job, or 404.
/// </summary>
[ApiController]
[Route("api/pipeline")]
[Authorize(Roles = "PafaAdmin")]
public class PipelineController : ControllerBase
{
    private readonly IPipelineBackgroundService _pipeline;
    private readonly IPipelineStateStore _stateStore;

    public PipelineController(
        IPipelineBackgroundService pipeline,
        IPipelineStateStore stateStore)
    {
        _pipeline   = pipeline;
        _stateStore = stateStore;
    }

    // ── POST /api/pipeline/run ────────────────────────────────────────────────

    /// <summary>
    /// Manually triggers the full ingestion pipeline for the specified period.
    /// Year and month default to the current UTC date if omitted.
    /// Returns 202 Accepted immediately; progress is streamed via SignalR (/hubs/pipeline).
    /// Returns 409 Conflict if a pipeline for the same month is already running.
    /// </summary>
    [HttpPost("run")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RunPipeline(
        [FromBody] RunPipelineRequest? request = null,
        CancellationToken ct = default)
    {
        var now   = DateTime.UtcNow;
        var year  = request?.Year  ?? now.Year;
        var month = request?.Month ?? now.Month;

        if (year  < 2020 || year  > 2040) return BadRequest("Année invalide (2020–2040).");
        if (month < 1    || month > 12)   return BadRequest("Mois invalide (1–12).");

        if (_stateStore.IsRunningForMonth(year, month))
            return Conflict(new { error = "A pipeline is already running for this month" });

        var jobId = Guid.NewGuid();
        var job   = new PipelineJob(jobId, year, month);

        await _pipeline.EnqueueAsync(job, ct);

        return Accepted(new
        {
            jobId  = jobId,
            status = "started",
            month  = $"{year}-{month:D2}"
        });
    }

    // ── GET /api/pipeline/status/{jobId} ─────────────────────────────────────

    /// <summary>
    /// Returns the full execution state for the given job.
    /// Clients may poll this endpoint as a fallback when SignalR is unavailable.
    /// </summary>
    [HttpGet("status/{jobId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetPipelineStatus(Guid jobId)
    {
        var state = _stateStore.Get(jobId);
        if (state is null)
            return NotFound(new { error = $"No pipeline job found with id '{jobId}'." });

        return Ok(state);
    }
}

// ── Request model ─────────────────────────────────────────────────────────────

/// <summary>Optional body for POST /api/pipeline/run.</summary>
public sealed record RunPipelineRequest(
    int? Year  = null,
    int? Month = null);
