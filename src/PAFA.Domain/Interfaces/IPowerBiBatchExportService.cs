// ═══════════════════════════════════════════════════════════
// PAFA.Domain/Interfaces/IPowerBiBatchExportService.cs
// PURPOSE: Contract for automated monthly batch export of
//          Power BI reports (41 reports) to Azure Blob Storage.
// ═══════════════════════════════════════════════════════════
namespace PAFA.Domain.Interfaces;

/// <summary>
/// Outcome of a single report export attempt.
/// </summary>
public record ReportExportOutcome(
    string ScheduleRef,
    string Title,
    bool Success,
    string? BlobPath,
    string? ErrorMessage,
    TimeSpan Duration);

/// <summary>
/// Aggregate result of a full batch export run.
/// </summary>
public record BatchExportResult(
    DateOnly ReportingPeriod,
    int TotalReports,
    int Succeeded,
    int Failed,
    TimeSpan TotalDuration,
    IReadOnlyList<ReportExportOutcome> Outcomes);

/// <summary>
/// Orchestrates the monthly batch export of all PARR Power BI reports.
/// Steps: dataset refresh (Import mode) → sequential PDF export → blob upload → DB tracking.
/// </summary>
public interface IPowerBiBatchExportService
{
    /// <summary>
    /// Exports all configured Power BI reports for the given reporting period.
    /// Each report failure is isolated — the batch continues with the next report.
    /// </summary>
    /// <param name="reportingPeriod">First day of the reporting month (e.g. 2026-03-01).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Aggregate result with per-report outcomes.</returns>
    Task<BatchExportResult> ExecuteMonthlyExportAsync(
        DateOnly reportingPeriod,
        CancellationToken ct = default);
}
