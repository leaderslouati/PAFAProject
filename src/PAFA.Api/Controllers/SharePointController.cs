using MediatR;
using Microsoft.AspNetCore.Mvc;
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
    IIngestionPipelineQueue queue) : ControllerBase
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
    /// Démarre le pipeline d'ingestion manuel complet pour la période demandée :
    ///   1. Identifie les fichiers SharePoint non encore traités avec succès.
    ///   2. Télécharge chaque fichier depuis SharePoint et le stocke dans MinIO.
    ///   3. Crée les enregistrements IngestionJob + IngestionFile en base.
    ///   4. Enfile chaque FileId dans le background worker (parse ? validate ? persist).
    ///
    /// Retourne 202 Accepted immédiatement — le traitement continue en arrière-plan.
    /// Le champ <c>enqueuedFileIds</c> contient les FileId passés au worker.
    /// </summary>
    [HttpPost("start")]
    [ProducesResponseType(typeof(StartIngestionResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(StartIngestionResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Start(
        [FromBody] InitiateSharePointFilesCommand cmd,
        CancellationToken ct = default)
    {
        if (cmd.Year < 2020 || cmd.Year > 2040)
            return BadRequest("L'année doit être comprise entre 2020 et 2040.");
        if (cmd.Month < 1 || cmd.Month > 12)
            return BadRequest("Le mois doit être compris entre 1 et 12.");

        // ?? Étape 1 + 2 + 3 : SharePoint ? MinIO ? Job/File en base ?????
        var initResult = await mediator.Send(cmd, ct);
        if (!initResult.Success)
        {
            return BadRequest(new StartIngestionResponse(
                Success: false,
                Year: cmd.Year, Month: cmd.Month,
                EnqueuedCount: 0,
                SkippedCount: initResult.SkippedFiles.Count,
                EnqueuedFileIds: [],
                SkippedFiles: initResult.SkippedFiles,
                ErrorMessage: initResult.ErrorMessage));
        }

        // ?? Étape 4 : Enfilage dans le background worker ?????????????????
        var enqueuedIds = new List<Guid>();
        foreach (var entry in initResult.PendingFiles)
        {
            await queue.EnqueueAsync(
                new PipelineFileMessage(entry.FileId, entry.FileName, entry.JobId), ct);
            enqueuedIds.Add(entry.FileId);
        }

        return Accepted(new StartIngestionResponse(
            Success: true,
            Year: cmd.Year, Month: cmd.Month,
            EnqueuedCount: enqueuedIds.Count,
            SkippedCount: initResult.SkippedFiles.Count,
            EnqueuedFileIds: enqueuedIds,
            SkippedFiles: initResult.SkippedFiles,
            ErrorMessage: null));
    }
}

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
