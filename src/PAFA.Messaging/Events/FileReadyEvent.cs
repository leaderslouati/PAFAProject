namespace PAFA.Messaging.Events;

/// <summary>
/// Publié par le Data Ingestion Service après upload du fichier brut en Blob/MinIO.
/// Consommé par le Processing &amp; Validation Service (FileReadyConsumer).
/// </summary>
public record FileReadyEvent
{
    public Guid IngestionJobId { get; init; }
    public Guid IngestionFileId { get; init; }
    public string FileName { get; init; } = "";
    public string BlobPath { get; init; } = "";
    public string SourceSystem { get; init; } = "CDSP";
    public int PeriodYear { get; init; }
    public int PeriodMonth { get; init; }
    public long FileSizeBytes { get; init; }
    public DateTime UploadedAt { get; init; } = DateTime.UtcNow;
}
