namespace PAFA.Extraction.Services;

/// <summary>
/// Message transporté dans la queue du pipeline d'ingestion.
/// Porte le contexte complet nécessaire au worker et aux notifications SignalR.
/// </summary>
public sealed record PipelineFileMessage(
    Guid FileId,
    string FileName,
    Guid JobId
);

/// <summary>
/// File d'attente thread-safe pour le pipeline d'ingestion.
/// Les messages sont publiés par le contrôleur HTTP (thread de la requête)
/// et consommés par le IngestionPipelineWorker (thread background).
///
/// Implémentée avec System.Threading.Channels (bounded, single-consumer).
/// </summary>
public interface IIngestionPipelineQueue
{
    /// <summary>Enfile un message à traiter. Non-bloquant.</summary>
    ValueTask EnqueueAsync(PipelineFileMessage message, CancellationToken ct = default);

    /// <summary>Attend et retourne le prochain message à traiter.</summary>
    ValueTask<PipelineFileMessage> DequeueAsync(CancellationToken ct = default);
}
