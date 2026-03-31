using PAFA.Domain.Enums;

namespace PAFA.Domain.Interfaces;

/// <summary>
/// Determines whether the current moment falls inside the automatic cron window
/// (days 18–21 of the month) and resolves the trigger mode accordingly.
/// </summary>
public interface IIngestionScheduleService
{
    /// <summary>
    /// Returns <see cref="TriggerMode.Automatic"/> when <paramref name="utcNow"/> (default: UTC now)
    /// falls inside the configured cron window; <see cref="TriggerMode.Manual"/> otherwise.
    /// </summary>
    TriggerMode ResolveTriggerMode(DateTime? utcNow = null);

    /// <summary>
    /// Returns a snapshot of the current schedule window state, used by
    /// <c>GET /api/ingest/schedule/status</c> to inform the frontend.
    /// </summary>
    ScheduleWindowStatus GetCurrentWindowStatus(DateTime? utcNow = null);
}

/// <summary>
/// Snapshot of the ingestion schedule window at a given point in time.
/// </summary>
public sealed record ScheduleWindowStatus(
    bool IsWithinWindow,
    int WindowStartDay,
    int WindowEndDay,
    int CurrentDay,
    TriggerMode TriggerMode,
    /// <summary>UTC date-time of the next window opening (1st second of WindowStartDay next month).</summary>
    DateTime NextWindowOpenAt,
    string CronExpression);
