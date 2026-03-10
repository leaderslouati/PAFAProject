using PAFA.Domain.Entities.ETL;
using PAFA.Domain.Enums;

namespace PAFA.Domain.Entities
{
    /// <summary>
    /// Details of each SFTP file processed in an ingestion job.
    /// Tracks the complete lifecycle: download → validation → loading.
    /// </summary>
    public class IngestionFile : BaseEntity
    {
        public Guid   Id { get; set; } = Guid.NewGuid();

        public Guid   IngestionJobId { get; set; }

        /// <summary>
        /// Exact source file name from Xoserve.
        /// E.g., "MOD520A__PAF_Reports_Mar25_Non Anonymised.xlsx"
        /// </summary>
        public string FileName       { get; set; } = string.Empty;

        public string   SourceSystem { get; set; } = "CDSP";   // CDSP | DDP | AD_HOC
        public FileType FileType     { get; set; } = FileType.Xlsx;

        public long?  FileSizeBytes  { get; set; }

        /// <summary>Azure Blob Storage path (Landing Zone).</summary>
        public string? BlobPath      { get; set; }

        /// <summary>SHA-256 file checksum for integrity verification.</summary>
        public string? Checksum      { get; set; }

        public IngestionFileStatus Status           { get; set; } = IngestionFileStatus.Downloaded;
        public ValidationStatus    ValidationStatus { get; set; } = ValidationStatus.Pending;

        public int?   RowsRead       { get; set; }
        public int?   RowsValid      { get; set; }
        public int?   RowsRejected   { get; set; }
        public int    ErrorCount     { get; set; } = 0;

        public DateTime? DownloadedAt { get; set; }
        public DateTime? ProcessedAt  { get; set; }

        // ── Navigation ──────────────────────────────────────────────────────
        public IngestionJob                  IngestionJob     { get; set; } = null!;
        public ICollection<ValidationError>  ValidationErrors { get; set; } = new List<ValidationError>();
    }
    
    
}