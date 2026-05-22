using Microsoft.AspNetCore.SignalR;

namespace PAFA.Api.Hubs;

/// <summary>
/// SignalR hub pour les notifications temps réel du pipeline d'ingestion.
///
/// Événements émis vers le front (noms des méthodes SignalR) :
///   "StepCompleted"  ? une étape vient de se terminer (succès ou échec)
///   "PipelineStarted" ? un nouveau pipeline a démarré (liste de fileId en attente)
///   "PipelineFinished" ? tous les fichiers d'un run sont traités
///
/// Le front écoute "StepCompleted" et met à jour son UI Processing Stages :
///   Step 1 — FileImport   (SP?MinIO, Status=Downloaded)
///   Step 2 — Parsing      (Status=Validating)
///   Step 3 — Validation   (ValidationStatus set)
///   Step 4 — Persistence  (Status=Loaded | Failed)
/// </summary>
public class IngestionHub : Hub
{
    private readonly ILogger<IngestionHub> _log;

    public IngestionHub(ILogger<IngestionHub> log) => _log = log;

    public override Task OnConnectedAsync()
    {
        _log.LogInformation("SignalR client connected: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _log.LogInformation("SignalR client disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}

// ?? Payloads SignalR ?????????????????????????????????????????????????????????

/// <summary>
/// Emis une fois par step, pour chaque fichier.
/// Le front utilise <see cref="FileId"/> + <see cref="Step"/> pour mettre à jour la bonne case.
/// </summary>
public sealed record StepCompletedPayload(
    /// <summary>Identifiant du fichier.</summary>
    Guid FileId,
    /// <summary>Nom du fichier (lisible pour le front).</summary>
    string FileName,
    /// <summary>Numéro de l'étape : 1=FileImport 2=Parsing 3=Validation 4=Persistence</summary>
    int Step,
    /// <summary>Label de l'étape : "FileImport" | "Parsing" | "Validation" | "Persistence"</summary>
    string StepName,
    /// <summary>"Success" | "Failed"</summary>
    string Status,
    /// <summary>Durée de l'étape en millisecondes.</summary>
    long DurationMs,
    /// <summary>Message d'erreur si Status = "Failed", null sinon.</summary>
    string? ErrorMessage = null,
    /// <summary>Données optionnelles propres à l'étape (ex: RowsRead, RowsValid…).</summary>
    Dictionary<string, object?>? Details = null
);

/// <summary>Emis quand un run démarre (après POST /api/sharepoint/start).</summary>
public sealed record PipelineStartedPayload(
    Guid JobId,
    int Year,
    int Month,
    int TotalFiles,
    IReadOnlyList<Guid> FileIds
);

/// <summary>Emis quand tous les fichiers d'un run sont traités.</summary>
public sealed record PipelineFinishedPayload(
    Guid JobId,
    int TotalFiles,
    int Succeeded,
    int Failed,
    long TotalDurationMs
);

/// <summary>
/// Emis par POST /api/sharepoint/start juste après la découverte des fichiers SharePoint
/// non encore traités et leur transfert dans MinIO.
/// Le front utilise cet événement pour afficher la liste "fichiers en attente" (Step 1).
/// </summary>
public sealed record PendingFilesDiscoveredPayload(
    int Year,
    int Month,
    int TotalPending,
    IReadOnlyList<PendingFileInfo> Files
);

/// <summary>Représente un fichier en attente de traitement (Step 1 — SharePoint ? MinIO).</summary>
public sealed record PendingFileInfo(
    Guid FileId,
    string FileName,
    long SizeBytes,
    /// <summary>"Pending" — fichier dans MinIO, en attente de parse+validate+persist.</summary>
    string Status
);

/// <summary>
/// Ligne de résultat par fichier pour Step 2 (ParseAndValidate fusionné).
/// Indique si le fichier ira dans le dossier "processed" ou "failed" de MinIO.
/// </summary>
public sealed record FileProcessingResultRow(
    Guid FileId,
    string FileName,
    /// <summary>"Processed" si valide, "Failed" si erreurs bloquantes ou parse échoué.</summary>
    string FileStatus,
    int RowsRead,
    int RowsValid,
    int RowsRejected,
    bool HasBlockingErrors,
    string? ErrorMessage
);

