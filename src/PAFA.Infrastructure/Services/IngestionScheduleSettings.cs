namespace PAFA.Infrastructure.Services;

/// <summary>
/// Typed configuration for the PAFA monthly ingestion schedule.
///
/// Automatic cron window: day CronWindowStartDay to day CronWindowEndDay (inclusive) of each month.
/// Outside that window the platform falls back to manual trigger mode.
/// </summary>
public sealed class IngestionScheduleSettings
{
    public const string SectionName = "IngestionSchedule";

    /// <summary>First day of the automatic cron window (inclusive). Default: 18.</summary>
    public int CronWindowStartDay { get; set; } = 18;

    /// <summary>Last day of the automatic cron window (inclusive). Default: 21.</summary>
    public int CronWindowEndDay { get; set; } = 21;

    /// <summary>Cron expression used by the hosted service. Default: 02:00 UTC on days 18–21.</summary>
    public string CronExpression { get; set; } = "0 2 18-21 * *";

    /// <summary>IANA time zone id for schedule evaluation. Default: UTC.</summary>
    public string TimeZone { get; set; } = "UTC";

    /// <summary>
    /// Returns true when <paramref name="utcNow"/>'s day falls inside
    /// [CronWindowStartDay, CronWindowEndDay].
    /// </summary>
    public bool IsWithinAutomaticWindow(DateTime utcNow)
        => utcNow.Day >= CronWindowStartDay && utcNow.Day <= CronWindowEndDay;
}
