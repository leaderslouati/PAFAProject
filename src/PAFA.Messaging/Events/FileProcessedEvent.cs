namespace PAFA.Messaging.Events;

/// <summary>
/// Publié après le processing complet (succès) d'un fichier.
/// Peut être consommé pour déclencher un refresh Power BI, une notification, etc.
/// </summary>
public record FileProcessedEvent
{
    public Guid IngestionJobId { get; init; }
    public Guid IngestionFileId { get; init; }
    public string FileName { get; init; } = "";
    public int RowsValid { get; init; }
    public int MetricsInserted { get; init; }
    public DateTime ProcessedAt { get; init; } = DateTime.UtcNow;
}
