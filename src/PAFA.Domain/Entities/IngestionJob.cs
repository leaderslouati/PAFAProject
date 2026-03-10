using PAFA.Domain.Enums;

namespace PAFA.Domain.Entities.ETL;

/// <summary>
/// Monthly ingestion job — represents a complete execution of the Xoserve SFTP pipeline.
/// One primary job per PARR cycle (unique constraint on PeriodYear + PeriodMonth).
/// </summary>
public class IngestionJob
{
    public Guid   Id             { get; set; } = Guid.NewGuid();

    /// <summary>Descriptive name (e.g., "PARR_INGESTION_2025_03").</summary>
    public string JobName        { get; set; } = string.Empty;

    public int    PeriodYear     { get; set; }
    public int    PeriodMonth    { get; set; }

    public IngestionJobStatus Status { get; set; } = IngestionJobStatus.Started;

    public int?   FilesExpected  { get; set; }
    public int    FilesDownloaded { get; set; } = 0;
    public int    FilesProcessed { get; set; } = 0;
    public int    FilesFailed    { get; set; } = 0;
    public long   RecordsLoaded  { get; set; } = 0;

    /// <summary>JSON summary of errors (e.g., {"missingFiles":["FILE_X"]}).</summary>
    public string? ErrorSummary  { get; set; }

    public int     RetryCount    { get; set; } = 0;

    public JobTrigger TriggeredBy { get; set; } = JobTrigger.Scheduler;

    public DateTime  StartedAt   { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    /// <summary>Reference to parent job in case of retry.</summary>
    public Guid?  ParentJobId   { get; set; }

    // ── Navigation ──────────────────────────────────────────────────────
    public IngestionJob?              ParentJob { get; set; }
    public ICollection<IngestionJob>  ChildJobs { get; set; } = new List<IngestionJob>();
    public ICollection<IngestionFile> Files     { get; set; } = new List<IngestionFile>();
}