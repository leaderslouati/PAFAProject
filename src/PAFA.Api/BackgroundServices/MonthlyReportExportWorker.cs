// ═══════════════════════════════════════════════════════════
// PAFA.Api/BackgroundServices/MonthlyReportExportWorker.cs
// PURPOSE: BackgroundService that triggers batch export of
//          41 Power BI reports on the 1st of each month
//          at 02:00 UTC.
//
//  The reporting period exported is always the PREVIOUS month
//  (e.g. worker runs 2026-04-01 → exports 2026-03 data).
//
//  Profile mapping (who sees what):
//    PafaAdmin / PafaUser → all 41 reports (SCH2A + SCH2B)
//    PacMember            → SCH2B only (22 reports)
//    Shipper              → SCH2A filtered by RLS (per-shipper, not batch)
//
//  The batch export runs as admin (no RLS filter).
//  Anonymisation is enforced at the SQL view level (v_parr_industry).
// ═══════════════════════════════════════════════════════════
using PAFA.Domain.Interfaces;
using PAFA.Infrastructure.Services.PowerBi;

namespace PAFA.Api.BackgroundServices;

public sealed class MonthlyReportExportWorker(
    IServiceScopeFactory scopeFactory,
    PowerBiBatchExportSettings settings,
    ILogger<MonthlyReportExportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.IsEnabled)
        {
            logger.LogInformation("MonthlyReportExportWorker is disabled (IsEnabled=false).");
            return;
        }

        // ── TEST MODE : TestTriggerDelayMinutes > 0 → fire once after N minutes ──
        if (settings.TestTriggerDelayMinutes > 0)
        {
            logger.LogWarning(
                "⚠ TEST MODE — MonthlyReportExportWorker will fire in {Delay} minute(s). " +
                "Remove TestTriggerDelayMinutes for production.",
                settings.TestTriggerDelayMinutes);

            try
            {
                await Task.Delay(
                    TimeSpan.FromMinutes(settings.TestTriggerDelayMinutes), stoppingToken);
            }
            catch (OperationCanceledException) { return; }

            await RunExportAsync(stoppingToken);
            logger.LogInformation("TEST MODE — single export done. Worker exiting.");
            return;
        }

        logger.LogInformation(
            "MonthlyReportExportWorker started — schedule: 1st of month, 02:00 UTC. " +
            "{Count} reports configured.",
            settings.Reports.Count);

        while (!stoppingToken.IsCancellationRequested)
        {
            // ── Wait until the next scheduled run ────────────────────
            var nextRun = ComputeNextRun(DateTime.UtcNow);
            var delay   = nextRun - DateTime.UtcNow;

            if (delay > TimeSpan.Zero)
            {
                logger.LogInformation("Next batch export scheduled at {NextRun:u}", nextRun);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            // ── Execute the export ──────────────────────────────────
            await RunExportAsync(stoppingToken);
        }

        logger.LogInformation("MonthlyReportExportWorker stopped.");
    }

    private async Task RunExportAsync(CancellationToken ct)
    {
        // The reporting period is the PREVIOUS month
        var now = DateTime.UtcNow;
        var reportingPeriod = new DateOnly(now.Year, now.Month, 1).AddMonths(-1);

        logger.LogInformation(
            "═══ Monthly Report Export triggered — period {Period:yyyy-MM} ═══",
            reportingPeriod);

        try
        {
            // Create a DI scope — the batch service depends on DbContext (scoped)
            using var scope = scopeFactory.CreateScope();
            var batchService = scope.ServiceProvider
                .GetRequiredService<IPowerBiBatchExportService>();

            var result = await batchService.ExecuteMonthlyExportAsync(reportingPeriod, ct);

            if (result.Failed > 0)
            {
                logger.LogWarning(
                    "Batch export completed with failures: " +
                    "{Succeeded}/{Total} succeeded, {Failed} failed in {Duration:F0}s",
                    result.Succeeded, result.TotalReports,
                    result.Failed, result.TotalDuration.TotalSeconds);
            }
            else
            {
                logger.LogInformation(
                    "Batch export completed: {Succeeded}/{Total} reports in {Duration:F0}s",
                    result.Succeeded, result.TotalReports,
                    result.TotalDuration.TotalSeconds);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            // Fatal error — log and wait for next month's run.
            // The worker does NOT crash the host.
            logger.LogCritical(ex,
                "Monthly batch export failed with unhandled exception.");
        }
    }

    /// <summary>
    /// Returns the 1st of the current or next month at 02:00 UTC.
    /// If we're already past the 1st at 02:00 this month, schedule for next month.
    /// </summary>
    private static DateTime ComputeNextRun(DateTime utcNow)
    {
        var firstOfThisMonth = new DateTime(
            utcNow.Year, utcNow.Month, 1, 2, 0, 0, DateTimeKind.Utc);

        return utcNow < firstOfThisMonth
            ? firstOfThisMonth
            : firstOfThisMonth.AddMonths(1);
    }
}
