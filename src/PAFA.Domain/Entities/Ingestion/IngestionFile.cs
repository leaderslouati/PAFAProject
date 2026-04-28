using PAFA.Domain.Enums;

namespace PAFA.Domain.Entities; 

/// <summary>
/// A single source file processed within an ingestion job.
/// Tracks the complete lifecycle: download → validation → loading.
/// </summary>
public class IngestionFile : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>FK → IngestionJob (parent job for this file).</summary>
    public Guid IngestionJobId { get; set; }

    public string FileName { get; set; } = string.Empty;

    /// <summary>CDSP | DDP | AD_HOC</summary>
    public string SourceSystem { get; set; } = "CDSP";

    public FileType FileType { get; set; } = FileType.Xlsx;

    public long? FileSizeBytes { get; set; }

    /// <summary>Azure Blob Storage path (Landing Zone).</summary>
    public string? BlobPath { get; set; }

    public string? FileHash { get; set; }

    public IngestionFileStatus Status { get; set; } = IngestionFileStatus.Downloaded;
    public ValidationStatus ValidationStatus { get; set; } = ValidationStatus.Pending;

    public int? RowsRead { get; set; }
    public int? RowsValid { get; set; }
    public int? RowsRejected { get; set; }
    public int ErrorCount { get; set; } = 0;

    public DateTime? DownloadedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }

    /// <summary>Last modified date from the remote source (SharePoint). Used for AC5 change detection.</summary>
    public DateTime? LastModifiedRemote { get; set; }

    // ── Navigation ─────────────────────────────────────────────────
    public IngestionJob IngestionJob { get; set; } = null!;

    public ICollection<ValidationError> ValidationErrors { get; set; }
        = new List<ValidationError>();

    public ICollection<MetricValue> MetricValues { get; set; }
        = new List<MetricValue>();
}