// ═══════════════════════════════════════════════════════════
// PAFA.Domain/Entities/IngestionFile.cs
//
// CORRECTIONS :
//   ✓ Namespace : PAFA.Domain.Entities (suppression de .ETL)
//   ✓ using PAFA.Domain.Entities.ETL supprimé (plus nécessaire)
//   ✓ Checksum renommé en FileHash (aligne avec FindByFileHashAsync)
//   ✓ Navigation ValidationErrors : type corrigé en ValidationError (singulier)
//   ✓ Navigation MetricValues ajoutée (inverse de MetricValue.IngestionFile)
// ═══════════════════════════════════════════════════════════
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

    /// <summary>
    /// Exact source filename from Xoserve.
    /// E.g. "MOD520A__PAF_Reports_Feb25_Non Anonymised.xlsx"
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>CDSP | DDP | AD_HOC</summary>
    public string SourceSystem { get; set; } = "CDSP";

    public FileType FileType { get; set; } = FileType.Xlsx;

    public long? FileSizeBytes { get; set; }

    /// <summary>Azure Blob Storage path (Landing Zone).</summary>
    public string? BlobPath { get; set; }

    /// <summary>
    /// SHA-256 file checksum for integrity verification and deduplication.
    /// Renamed from Checksum → FileHash to align with IIngestionJobRepository.FindByFileHashAsync.
    /// </summary>
    public string? FileHash { get; set; }

    public IngestionFileStatus Status { get; set; } = IngestionFileStatus.Downloaded;
    public ValidationStatus ValidationStatus { get; set; } = ValidationStatus.Pending;

    public int? RowsRead { get; set; }
    public int? RowsValid { get; set; }
    public int? RowsRejected { get; set; }
    public int ErrorCount { get; set; } = 0;

    public DateTime? DownloadedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }

    // ── Navigation ─────────────────────────────────────────────────
    public IngestionJob IngestionJob { get; set; } = null!;

    /// <summary>
    /// Validation errors raised during processing of this file.
    /// Type corrected to ValidationError (singulier — convention EF Core).
    /// </summary>
    public ICollection<ValidationError> ValidationErrors { get; set; }
        = new List<ValidationError>();

    /// <summary>
    /// Metric values extracted from this file after successful validation.
    /// </summary>
    public ICollection<MetricValue> MetricValues { get; set; }
        = new List<MetricValue>();
}