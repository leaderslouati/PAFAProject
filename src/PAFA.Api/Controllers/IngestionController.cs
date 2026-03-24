using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PAFA.Domain.Enums;
using PAFA.Domain.Interfaces;
using PAFA.Extraction.Commands.SharePoint;
using System.Security.Claims;

namespace PAFA.Api.Controllers;

[ApiController]
[Route("api/ingest")]
public class IngestionController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IIngestionScheduleService _schedule;

    public IngestionController(IMediator mediator, IIngestionScheduleService schedule)
    {
        _mediator = mediator;
        _schedule = schedule;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  POST /api/ingest  — manual trigger (runs the full pipeline)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Manually triggers the SharePoint download + validation + import pipeline.
    /// Runs the same complete flow as the automatic cron trigger.
    /// Available at any time — inside or outside the automatic window [day 18–21].
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "PafaAdmin")]
    [ProducesResponseType(typeof(IngestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IngestResponse), StatusCodes.Status207MultiStatus)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(IngestResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Ingest(
        [FromQuery] int? year  = null,
        [FromQuery] int? month = null,
        CancellationToken ct   = default)
    {
        if (year.HasValue  && (year  < 2020 || year  > 2040)) return BadRequest("Année invalide.");
        if (month.HasValue && (month < 1    || month > 12))   return BadRequest("Mois invalide.");

        var triggerMode  = _schedule.ResolveTriggerMode();
        var windowStatus = _schedule.GetCurrentWindowStatus();

        var result = await _mediator.Send(new DownloadParrFilesCommand(
            Year:          year,
            Month:         month,
            TriggerSource: "MANUAL_API",
            TriggerMode:   triggerMode), ct);

        return BuildResponse(result, windowStatus);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  POST /api/ingest/reprocess  — re-trigger after corrections
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Re-triggers validation and processing for a period after file corrections.
    /// The new job is linked to the previous failed job (ParentJobId / RetryCount).
    /// Optionally restrict to a subset of files via FileNameFilter.
    /// </summary>
    [HttpPost("reprocess")]
    [Authorize(Roles = "PafaAdmin")]
    [ProducesResponseType(typeof(IngestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(IngestResponse), StatusCodes.Status207MultiStatus)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(IngestResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Reprocess(
        [FromBody] ReprocessRequest request,
        CancellationToken ct = default)
    {
        if (request.Year  < 2020 || request.Year  > 2040) return BadRequest("Année invalide.");
        if (request.Month < 1    || request.Month > 12)   return BadRequest("Mois invalide.");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub") ?? "unknown";
        var triggerMode  = _schedule.ResolveTriggerMode();
        var windowStatus = _schedule.GetCurrentWindowStatus();

        HttpContext.RequestServices
            .GetRequiredService<ILogger<IngestionController>>()
            .LogInformation(
                "AUDIT | Manual reprocess triggered | Period={Year}-{Month:D2} | Files={Filter} | TriggeredBy={User}",
                request.Year, request.Month,
                request.FileNameFilter is { Count: > 0 }
                    ? string.Join(",", request.FileNameFilter) : "*",
                userId);

        var result = await _mediator.Send(new DownloadParrFilesCommand(
            Year:           request.Year,
            Month:          request.Month,
            FileNameFilter: request.FileNameFilter,
            TriggerSource:  "MANUAL_REPROCESS",
            TriggerMode:    triggerMode), ct);

        return BuildResponse(result, windowStatus);
    }

    // ══════════════════════════════════════════════════════════════════════
    //  GET /api/ingest/schedule/status
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the current cron-window state so the frontend can decide
    /// whether the manual trigger button should be displayed.
    /// </summary>
    [HttpGet("schedule/status")]
    [ProducesResponseType(typeof(ScheduleStatusResponse), StatusCodes.Status200OK)]
    public IActionResult GetScheduleStatus()
    {
        var s = _schedule.GetCurrentWindowStatus();
        return Ok(new ScheduleStatusResponse(
            s.IsWithinWindow,
            s.WindowStartDay,
            s.WindowEndDay,
            s.CurrentDay,
            s.TriggerMode.ToString(),
            s.NextWindowOpenAt,
            s.CronExpression,
            s.IsWithinWindow
                ? "Automatic cron is active. Manual trigger is available but not required."
                : $"Outside automatic window (days {s.WindowStartDay}–{s.WindowEndDay}). Manual trigger required."));
    }

    // ── Shared helper ─────────────────────────────────────────────────────

    private IActionResult BuildResponse(
        DownloadParrFilesResult result,
        ScheduleWindowStatus windowStatus)
    {
        var response = new IngestResponse(
            result.Success,
            result.FilesDownloaded,
            result.FilesImported,
            result.FilesFailed,
            result.ImportedFiles,
            result.Errors.Select(e => new IngestError(e.FileName, e.ErrorMessage)).ToList(),
            result.SkippedFiles.Select(s =>
                new IngestSkipped(s.FileName, s.RuleId, s.Reason, s.SkippedAt)).ToList(),
            result.TriggerSource,
            result.TriggerMode,
            windowStatus.IsWithinWindow);

        if (!result.Success && result.FilesImported == 0)
            return StatusCode(StatusCodes.Status500InternalServerError, response);

        return result.FilesFailed > 0 || result.SkippedFiles.Count > 0
            ? StatusCode(StatusCodes.Status207MultiStatus, response)
            : Ok(response);
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────

public record ReprocessRequest(
    int Year,
    int Month,
    List<string>? FileNameFilter = null);

public record IngestResponse(
    bool Success,
    int FilesDownloaded,
    int FilesImported,
    int FilesFailed,
    List<string> ImportedFiles,
    List<IngestError> Errors,
    List<IngestSkipped> SkippedFiles,
    string TriggerSource,
    string TriggerMode,
    bool IsWithinAutomaticWindow);

public record IngestError(string FileName, string ErrorMessage);

public record IngestSkipped(string FileName, string RuleId, string Reason, DateTime SkippedAt);

public record ScheduleStatusResponse(
    bool IsWithinWindow,
    int WindowStartDay,
    int WindowEndDay,
    int CurrentDay,
    string TriggerMode,
    DateTime NextWindowOpenAt,
    string CronExpression,
    string Message);
