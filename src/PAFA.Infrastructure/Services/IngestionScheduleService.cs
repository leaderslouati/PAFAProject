using Microsoft.Extensions.Options;
using PAFA.Domain.Enums;
using PAFA.Domain.Interfaces;

namespace PAFA.Infrastructure.Services;

/// <summary>
/// Evaluates the PAFA ingestion schedule window [day 18 – day 21] and
/// resolves the appropriate <see cref="TriggerMode"/> for a given UTC instant.
/// </summary>
public sealed class IngestionScheduleService(
    IOptions<IngestionScheduleSettings> options) : IIngestionScheduleService
{
    private readonly IngestionScheduleSettings _cfg = options.Value;

    /// <inheritdoc />
    public TriggerMode ResolveTriggerMode(DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        return _cfg.IsWithinAutomaticWindow(now)
            ? TriggerMode.Automatic
            : TriggerMode.Manual;
    }

    /// <inheritdoc />
    public ScheduleWindowStatus GetCurrentWindowStatus(DateTime? utcNow = null)
    {
        var now    = utcNow ?? DateTime.UtcNow;
        var mode   = ResolveTriggerMode(now);
        var nextWindowOpenAt = ComputeNextWindowOpen(now);

        return new ScheduleWindowStatus(
            IsWithinWindow: mode == TriggerMode.Automatic,
            WindowStartDay: _cfg.CronWindowStartDay,
            WindowEndDay:   _cfg.CronWindowEndDay,
            CurrentDay:     now.Day,
            TriggerMode:    mode,
            NextWindowOpenAt: nextWindowOpenAt,
            CronExpression: _cfg.CronExpression);
    }

    // ?? Helpers ??????????????????????????????????????????????????????????

    /// <summary>
    /// Returns the UTC date-time of the first second of the next cron window opening.
    /// If today is before the window start ? window starts this month.
    /// If today is inside or past the window ? window starts next month.
    /// </summary>
    private DateTime ComputeNextWindowOpen(DateTime now)
    {
        // Still before the window this month
        if (now.Day < _cfg.CronWindowStartDay)
            return new DateTime(now.Year, now.Month, _cfg.CronWindowStartDay,
                0, 0, 0, DateTimeKind.Utc);

        // Inside or past the window ? next month
        var nextMonth = now.AddMonths(1);
        return new DateTime(nextMonth.Year, nextMonth.Month, _cfg.CronWindowStartDay,
            0, 0, 0, DateTimeKind.Utc);
    }
}
