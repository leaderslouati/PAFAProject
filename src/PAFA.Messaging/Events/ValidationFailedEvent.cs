namespace PAFA.Messaging.Events;

/// <summary>
/// Publié quand la validation d'un fichier échoue.
/// Consommé pour pousser une alerte temps réel via SignalR au frontend.
/// </summary>
public record ValidationFailedEvent
{
    public Guid IngestionFileId { get; init; }
    public string FileName { get; init; } = "";
    public string ErrorMessage { get; init; } = "";
    public int ErrorCount { get; init; }
    public DateTime FailedAt { get; init; } = DateTime.UtcNow;
}
