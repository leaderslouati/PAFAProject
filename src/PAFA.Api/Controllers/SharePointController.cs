using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using PAFA.Api.Hubs;
using PAFA.Extraction.Commands.SharePoint;
using PAFA.Extraction.Services;

namespace PAFA.Api.Controllers;

/// <summary>
/// Endpoints pour le déclenchement MANUEL de l'ingestion (hors fenêtre cron 18–21).
///
/// ??? Flux recommandé ?????????????????????????????????????????????????????????
///
///   1. (Optionnel) GET /api/sharepoint/pending-files?year=2025&amp;month=7
///      ? Consulte les fichiers SharePoint non encore traités. Lecture seule.
///
///   2. POST /api/sharepoint/start
///      ? Identifie les fichiers pending sur SharePoint, les transfère dans MinIO,
///        crée les enregistrements Job + IngestionFile en base, puis enfile les
///        fileId dans le background worker qui exécute parse ? validate ? persist.
///      ? Retourne 202 Accepted immédiatement avec la liste des fileId enqueués.
///        Le traitement se poursuit en arrière-plan.
///
///   3. Suivi : GET /api/files/{fileId}/status (ou GET /api/validation/job/{jobId})
///      ? Consulte l'état d'avancement d'un fichier.
///
/// ??? Pourquoi un background worker ? ????????????????????????????????????????
///   - FilePipelineCache est Scoped ? parse, validate et persist doivent
///     s'exécuter dans le MÊME scope DI pour partager le cache des rows parsées.
///   - Le worker crée un scope isolé par fichier, garantissant l'isolation
///     du DbContext et du cache entre les fichiers traités en séquence.
/// </summary>
[ApiController]
[Route("api/sharepoint")]
public class SharePointController(
    IMediator mediator,
    IIngestionPipelineQueue queue,
    IHubContext<IngestionHub> hub) : ControllerBase
{
    /// <summary>
    /// Lecture seule — liste les fichiers SharePoint disponibles pour la période
    /// en distinguant ceux déjà traités (Loaded) de ceux encore en attente.
    /// Aucune écriture en base, aucun transfert MinIO.
    /// </summary>
    [HttpGet("pending-files")]
    [ProducesResponseType(typeof(ListSharePointPendingFilesResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPendingFiles(
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken ct = default)
    {
        if (year < 2020 || year > 2040)
            return BadRequest("L'année doit être comprise entre 2020 et 2040.");
        if (month < 1 || month > 12)
            return BadRequest("Le mois doit être compris entre 1 et 12.");

        var result = await mediator.Send(new ListSharePointPendingFilesQuery(year, month), ct);
        return Ok(result);
    }

    /// <summary>
    /// Démarre le pipeline d'ingestion pour la période courante (ou celle spécifiée).
    ///
    /// - Year et Month sont optionnels : par défaut = mois/année courants du système.
    /// - Les fichiers neufs sont dans /{BaseInboundPath}/{YYYY}/{MM}/.
    /// - Les fichiers dans /processed ou /failed sont ignorés SAUF si reprocessFailed=true,
    ///   auquel cas ils sont déplacés vers /inbound avant de passer dans le pipeline.
    ///
    /// Retourne 202 Accepted immédiatement.
    /// </summary>
    [HttpPost("start")]
    [ProducesResponseType(typeof(StartIngestionResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(StartIngestionResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Start(
        [FromBody] StartIngestionRequest? body = null,
        CancellationToken ct = default)
    {
        // Use the current UTC year/month for ingestion run. The API does not accept
        // an explicit period in the request body — the ingestion always targets the
        // current period.
        var now = DateTime.UtcNow;
        var year = now.Year;
        var month = now.Month;

        // For ingestion API we always process the selected period (or current period if absent)
        // The client does not need to supply fileNameFilter or reprocessFailed — ingestion will
        // consider all eligible files for the period and will not reprocess previously failed files
        // unless reprocess is implemented as a separate endpoint.
        var cmd = new InitiateSharePointFilesCommand(
            Year: year,
            Month: month,
            FileNameFilter: null,
            ReprocessFailed: false);

        // Étape 1+2+3 : SharePoint ? MinIO ? Job/File en base
        var initResult = await mediator.Send(cmd, ct);
        if (!initResult.Success)
        {
            return BadRequest(new StartIngestionResponse(
                Success: false,
                Year: year, Month: month,
                EnqueuedCount: 0,
                SkippedCount: initResult.SkippedFiles.Count,
                EnqueuedFileIds: [],
                SkippedFiles: initResult.SkippedFiles,
                ErrorMessage: initResult.ErrorMessage));
        }

        // Étape 4 : émettre "PendingFilesDiscovered" vers le front
        var pendingInfos = initResult.PendingFiles
            .Select(e => new PendingFileInfo(e.FileId, e.FileName, e.SizeBytes, "Pending"))
            .ToList();

        await hub.Clients.All.SendAsync("PendingFilesDiscovered", new PendingFilesDiscoveredPayload(
            Year:         year,
            Month:        month,
            TotalPending: pendingInfos.Count,
            Files:        pendingInfos), ct);

        // Étape 5 : enfilage dans le background worker
        var enqueuedIds = new List<Guid>();
        foreach (var entry in initResult.PendingFiles)
        {
            await queue.EnqueueAsync(
                new PipelineFileMessage(entry.FileId, entry.FileName, entry.JobId), ct);
            enqueuedIds.Add(entry.FileId);
        }

        return Accepted(new StartIngestionResponse(
            Success: true,
            Year: year, Month: month,
            EnqueuedCount: enqueuedIds.Count,
            SkippedCount: initResult.SkippedFiles.Count,
            EnqueuedFileIds: enqueuedIds,
            SkippedFiles: initResult.SkippedFiles,
            ErrorMessage: null));
    }
}

/// <summary>
/// Corps optionnel de POST /api/sharepoint/start.
/// Tous les champs sont optionnels — si absent, le mois/année courants sont utilisés.
/// </summary>
public sealed record StartIngestionRequest(
    /// <summary>Année de la période. Défaut = année courante.</summary>
    int? Year = null,
    /// <summary>Mois de la période (1-12). Défaut = mois courant.</summary>
    int? Month = null
);

/// <summary>Réponse de POST /api/sharepoint/start (202 Accepted).</summary>
public sealed record StartIngestionResponse(
    bool Success,
    int Year,
    int Month,
    /// <summary>Nombre de fichiers enfilés dans le background worker.</summary>
    int EnqueuedCount,
    /// <summary>Nombre de fichiers ignorés (déjà Loaded ou nom invalide).</summary>
    int SkippedCount,
    /// <summary>
    /// FileId enfilés — le client peut suivre l'état via
    /// GET /api/validation/job/{jobId} ou GET /api/files/{fileId}/status.
    /// </summary>
    IReadOnlyList<Guid> EnqueuedFileIds,
    IReadOnlyList<SkippedFileRecord> SkippedFiles,
    string? ErrorMessage
);
