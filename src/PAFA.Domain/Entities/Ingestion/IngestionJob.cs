using PAFA.Domain.Enums;

namespace PAFA.Domain.Entities;

/// <summary>
/// Monthly ingestion job — one per PARR cycle.
/// Tracks the full lifecycle of the SFTP download + processing pipeline.
/// </summary>
public class IngestionJob : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Descriptive name (e.g. "PARR_INGESTION_2025_02").</summary>
    public string JobName { get; set; } = string.Empty;
    public DateOnly ReportingPeriod { get; set; }

    public IngestionJobStatus Status { get; set; } = IngestionJobStatus.Started;

    public int? FilesExpected { get; set; }
    public int FilesDownloaded { get; set; } = 0;
    public int FilesProcessed { get; set; } = 0;
    public int FilesFailed { get; set; } = 0;
    public long RecordsLoaded { get; set; } = 0;

    /// <summary>JSON summary of errors (e.g. {"missingFiles":["FILE_X"]}).</summary>
    public string? ErrorSummary { get; set; }

    public int RetryCount { get; set; } = 0;

    public JobTrigger TriggeredBy { get; set; } = JobTrigger.Scheduler;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    /// <summary>Reference to parent job in case of retry.</summary>
    public Guid? ParentJobId { get; set; }

    /// <summary>Correlation ID linking this job execution to all pipeline logs and notifications.</summary>
    public Guid? CorrelationId { get; set; }

    // ── Navigation ─────────────────────────────────────────────────
    public IngestionJob? ParentJob { get; set; }
    public ICollection<IngestionJob> RetryJobs { get; set; } = new List<IngestionJob>();
    public ICollection<IngestionFile> IngestionFiles { get; set; } = new List<IngestionFile>();
}