// ═══════════════════════════════════════════════════════════
// PAFA.Infrastructure/Services/PowerBi/PowerBiBatchExportSettings.cs
// PURPOSE: Strongly-typed configuration for the batch export
//          of 41 Power BI reports (19 × SCH2A + 22 × SCH2B).
//          Bound from appsettings.json § "PowerBiBatchExport".
// ═══════════════════════════════════════════════════════════
namespace PAFA.Infrastructure.Services.PowerBi;

public sealed class PowerBiBatchExportSettings
{
    public const string SectionName = "PowerBiBatchExport";

    /// <summary>Master switch — false disables the BackgroundService.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// TEST ONLY — fire the export once after N minutes from startup.
    /// Set to 0 (default) for normal production scheduling (1st of month, 02:00 UTC).
    /// </summary>
    public int TestTriggerDelayMinutes { get; set; } = 0;

    // ── Throttling ───────────────────────────────────────────────
    /// <summary>Number of reports to export before inserting a throttle pause.</summary>
    public int BatchSize { get; set; } = 5;

    /// <summary>Seconds to pause between batches (Power BI API limit: 5 concurrent exports / tenant).</summary>
    public int ThrottleDelaySeconds { get; set; } = 10;

    // ── Blob Storage ─────────────────────────────────────────────
    /// <summary>Target blob container for exported PDFs.</summary>
    public string BlobContainer { get; set; } = "reports";

    // ── Dataset Refresh (Import Mode) ────────────────────────────
    /// <summary>Max wait for a dataset refresh to complete. Default: 10 min.</summary>
    public int DatasetRefreshTimeoutMinutes { get; set; } = 10;

    /// <summary>Polling interval while waiting for refresh. Default: 15 s.</summary>
    public int RefreshPollIntervalSeconds { get; set; } = 15;

    // ── Report Export ────────────────────────────────────────────
    /// <summary>Max wait for a single PDF export. Default: 5 min (300 s).</summary>
    public int ExportTimeoutSeconds { get; set; } = 300;

    /// <summary>Polling interval while waiting for export. Default: 5 s.</summary>
    public int ExportPollIntervalSeconds { get; set; } = 5;

    // ── Dataset definitions ──────────────────────────────────────
    /// <summary>Import-mode datasets to refresh before exporting reports.</summary>
    public List<DatasetDefinition> Datasets { get; set; } = [];

    // ── Report definitions (41 entries) ──────────────────────────
    /// <summary>All Power BI reports to export, with schedule metadata.</summary>
    public List<ReportDefinition> Reports { get; set; } = [];
}

/// <summary>
/// A Power BI dataset (Import mode) that must be refreshed before exporting
/// the reports that depend on it.
/// </summary>
public sealed class DatasetDefinition
{
    /// <summary>Power BI Dataset GUID.</summary>
    public string DatasetId { get; set; } = string.Empty;

    /// <summary>Human-readable label for logging.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// True = trigger refresh + poll. False = skip (e.g. DirectQuery dataset).
    /// Set to true for Import-mode datasets used by batch export.
    /// </summary>
    public bool RequiresRefresh { get; set; } = true;
}

/// <summary>
/// Maps a Power BI report to its PARR schedule metadata.
/// Used to build the blob filename and track in the Report entity.
/// </summary>
public sealed class ReportDefinition
{
    /// <summary>Power BI Report GUID (from the workspace).</summary>
    public string PowerBiReportId { get; set; } = string.Empty;

    /// <summary>Schedule reference (e.g. "2A.1", "2B.14").</summary>
    public string ScheduleRef { get; set; } = string.Empty;

    /// <summary>Full title (e.g. "Estimated and Check Reads – Products 1 and 2").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>"Industry" (SCH2A) or "PAC" (SCH2B).</summary>
    public string Audience { get; set; } = "Industry";

    /// <summary>Schedule number within the type (1-19 for 2A, 1-22 for 2B).</summary>
    public int ScheduleNumber { get; set; }

    /// <summary>Matches ReportType.Code in DB ("SCH2A" or "SCH2B").</summary>
    public string ReportTypeCode { get; set; } = "SCH2A";
}
