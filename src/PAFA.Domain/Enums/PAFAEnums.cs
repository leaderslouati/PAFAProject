namespace PAFA.Domain.Enums;

public enum IngestionJobStatus
{
    Started, Processing, Completed, Failed, PartiallyCompleted, Cancelled
}

public enum IngestionFileStatus
{
    Downloaded, Validating, Valid, Invalid, Processed, Failed
}

public enum ValidationStatus
{
    Pending, Valid, PassedWithWarnings, Failed
}

public enum FileType
{
    Xlsx, Xls, Csv, Xml
}

public enum JobTrigger
{
    Scheduler, Manual, Api, Retry
}

/// <summary>
/// Indicates whether a pipeline run was started by the automatic cron
/// (day 18–21 of the month) or by a manual user action outside that window.
/// </summary>
public enum TriggerMode
{
    /// <summary>Cron window is active — run was started automatically.</summary>
    Automatic,
    /// <summary>Outside the cron window — run was started by a user.</summary>
    Manual
}

public enum ReportStatus
{
    Pending, Generating, Generated, Published, Archived, Failed
}

public enum ReportAudience
{
    Industry,   // 2A — anonymisé
    PAC         // 2B — non-anonymisé
}

public enum ExportFormat
{
    Csv, Excel, Pdf, PowerBiEmbedded
}

/// <summary>Outcome of a single file during the Import step (Step 1).</summary>
public enum ImportStatus
{
    Imported, SkippedInvalidName, SkippedInvalidFolder, SkippedAlreadyProcessed
}

/// <summary>Overall status of a pipeline execution tracked by PipelineBackgroundService.</summary>
public enum PipelineStatus
{
    Pending, Running, Success, Error
}

/// <summary>Status of a single step inside a pipeline execution.</summary>
public enum StepStatus
{
    Pending, Running, Success, Error, Skipped
}