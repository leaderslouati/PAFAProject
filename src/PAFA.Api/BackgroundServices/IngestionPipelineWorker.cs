using MediatR;
using Microsoft.AspNetCore.SignalR;
using PAFA.Api.Hubs;
using PAFA.Extraction.Commands.Import;
using PAFA.Extraction.Services;

namespace PAFA.Api.BackgroundServices;

/// <summary>
/// Background worker qui consomme la <see cref="IIngestionPipelineQueue"/>
/// et exécute les 3 étapes du pipeline pour chaque fichier reçu, en émettant
/// des événements SignalR après chaque étape.
///
/// ??? Étapes du pipeline ???????????????????????????????????????????????????????
///   Step 1 — SharePointToMinIO : fichier déjà dans MinIO (fait par /start).
///             ? Notifié immédiatement. Retour JSON : liste des fichiers non traités.
///   Step 2 — ParseAndValidate  : Parse + Validation fusionnés dans la même étape.
///             ? Résultat : rows[] avec {fileName, status("Processed"|"Failed")}
///             ? Déclenché automatiquement. Sauvegarde les fichiers valides en blob.
///   Step 3 — Persistence       : PersistFileCommand — insertion en base avec le bon statut.
///
/// Événements SignalR émis :
///   "PendingFilesDiscovered" ? Step 1 : liste JSON des fichiers en attente
///   "StepCompleted"          ? après chaque étape (succès ou échec)
///   "PipelineFinished"       ? quand tous les fichiers du run sont traités
/// </summary>
public sealed class IngestionPipelineWorker(
    IIngestionPipelineQueue queue,
    IServiceScopeFactory scopeFactory,
    IHubContext<IngestionHub> hub,
    ILogger<IngestionPipelineWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[PIPELINE_WORKER] Démarré — en attente de fichiers à traiter.");

        while (!stoppingToken.IsCancellationRequested)
        {
            PipelineFileMessage msg;
            try
            {
                msg = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await ProcessOneFileAsync(msg, stoppingToken);
        }

        logger.LogInformation("[PIPELINE_WORKER] Arrêté.");
    }

    private async Task ProcessOneFileAsync(PipelineFileMessage msg, CancellationToken ct)
    {
        var (fileId, fileName, jobId) = msg;
        var runStart = DateTime.UtcNow;

        logger.LogInformation(
            "[PIPELINE_WORKER] Démarrage — FileId={FileId} FileName={File}",
            fileId, fileName);

        // ?? Chaque fichier = scope DI isolé ??????????????????????????????????
        // DbContext + FilePipelineCache neufs ? pas de conflit entre fichiers
        using var scope = scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // ?? Step 1 : SharePoint ? MinIO (déjà fait par /start) ??????????????
        // On notifie immédiatement que le fichier est prêt dans MinIO.
        // Le payload "PendingFilesDiscovered" a déjà été émis par le contrôleur ;
        // ici on confirme que ce fichier spécifique entre dans le worker.
        var d1 = Elapsed(runStart);
        await NotifyStep(fileId, fileName, step: 1, stepName: "SharePointToMinIO",
            success: true, durationMs: d1,
            details: new() { ["blobReady"] = true, ["fileName"] = fileName }, ct: ct);

        // ?? Step 2 : Parse + Validate (fusionnés) ????????????????????????????
        // Parse le fichier depuis MinIO, applique les règles de validation,
        // puis déplace le blob vers "processed" ou "failed" selon le résultat.
        var t2 = DateTime.UtcNow;

        var parseResult   = await mediator.Send(new ParseFileCommand(fileId), ct);
        var d2Parse       = Elapsed(t2);

        if (!parseResult.Success)
        {
            logger.LogWarning("[PIPELINE_WORKER] Parse échoué — {File} | {Err}", fileName, parseResult.ErrorMessage);

            var failedRow = new FileProcessingResultRow(
                FileId:            fileId,
                FileName:          fileName,
                FileStatus:        "Failed",
                RowsRead:          0,
                RowsValid:         0,
                RowsRejected:      0,
                HasBlockingErrors: true,
                ErrorMessage:      parseResult.ErrorMessage);

            await NotifyStep(fileId, fileName, step: 2, stepName: "ParseAndValidate",
                success: false, durationMs: d2Parse,
                error: parseResult.ErrorMessage,
                details: new() { ["result"] = failedRow }, ct: ct);

            await NotifyFinished(jobId, fileId, succeeded: false, runStart, ct);
            return;
        }

        var validateResult = await mediator.Send(new ValidateFileCommand(fileId), ct);
        var d2             = Elapsed(t2);

        var fileStatus = validateResult.HasBlockingErrors ? "Failed" : "Processed";

        var resultRow = new FileProcessingResultRow(
            FileId:            fileId,
            FileName:          fileName,
            FileStatus:        fileStatus,
            RowsRead:          parseResult.TotalRows,
            RowsValid:         validateResult.ValidRowCount,
            RowsRejected:      validateResult.InvalidRowCount,
            HasBlockingErrors: validateResult.HasBlockingErrors,
            ErrorMessage:      validateResult.HasBlockingErrors ? validateResult.ErrorMessage : null);

        await NotifyStep(fileId, fileName, step: 2, stepName: "ParseAndValidate",
            success: validateResult.Success, durationMs: d2,
            error: validateResult.HasBlockingErrors ? validateResult.ErrorMessage : null,
            details: new()
            {
                ["result"]            = resultRow,
                ["rowsRead"]          = parseResult.TotalRows,
                ["rowsValid"]         = validateResult.ValidRowCount,
                ["rowsRejected"]      = validateResult.InvalidRowCount,
                ["hasBlockingErrors"] = validateResult.HasBlockingErrors,
                ["fileStatus"]        = fileStatus
            },
            ct: ct);

        if (!validateResult.Success)
        {
            logger.LogWarning("[PIPELINE_WORKER] Validate échoué — {File} | {Err}", fileName, validateResult.ErrorMessage);
            await NotifyFinished(jobId, fileId, succeeded: false, runStart, ct);
            return;
        }

        // ?? Step 3 : Persistence en base de données ??????????????????????????
        // Insère les MetricValues pour les lignes valides et finalise le statut.
        var t3 = DateTime.UtcNow;
        var persistResult = await mediator.Send(new PersistFileCommand(fileId), ct);
        var d3 = Elapsed(t3);

        var succeeded = persistResult.Success;

        await NotifyStep(fileId, fileName, step: 3, stepName: "Persistence",
            success: persistResult.Success, durationMs: d3,
            error: persistResult.ErrorMessage,
            details: persistResult.Success
                ? new()
                {
                    ["metricsInserted"] = persistResult.MetricsInserted,
                    ["blobPath"]        = persistResult.FinalBlobPath,
                    ["fileStatus"]      = "Processed"
                }
                : new Dictionary<string, object?>
                {
                    ["fileStatus"] = "Failed",
                    ["error"]      = persistResult.ErrorMessage
                },
            ct: ct);

        await NotifyFinished(jobId, fileId, succeeded, runStart, ct);

        if (succeeded)
            logger.LogInformation(
                "[PIPELINE_WORKER] ? {File} — Read={Read} Valid={Valid} Metrics={Metrics} ({TotalMs}ms)",
                fileName, parseResult.TotalRows, validateResult.ValidRowCount,
                persistResult.MetricsInserted, Elapsed(runStart));
        else
            logger.LogWarning(
                "[PIPELINE_WORKER] ? {File} — Persist échoué : {Err}", fileName, persistResult.ErrorMessage);
    }

    // ?? Helpers ???????????????????????????????????????????????????????????????

    private Task NotifyStep(
        Guid fileId, string fileName, int step, string stepName,
        bool success, long durationMs,
        string? error = null, Dictionary<string, object?>? details = null,
        CancellationToken ct = default)
    {
        var payload = new StepCompletedPayload(
            FileId:       fileId,
            FileName:     fileName,
            Step:         step,
            StepName:     stepName,
            Status:       success ? "Success" : "Failed",
            DurationMs:   durationMs,
            ErrorMessage: error,
            Details:      details);

        return hub.Clients.All.SendAsync("StepCompleted", payload, ct);
    }

    private Task NotifyFinished(Guid jobId, Guid fileId, bool succeeded, DateTime runStart, CancellationToken ct)
        => hub.Clients.All.SendAsync("PipelineFinished", new PipelineFinishedPayload(
            JobId:           jobId,
            TotalFiles:      1,
            Succeeded:       succeeded ? 1 : 0,
            Failed:          succeeded ? 0 : 1,
            TotalDurationMs: Elapsed(runStart)), ct);

    private static long Elapsed(DateTime since)
        => (long)(DateTime.UtcNow - since).TotalMilliseconds;
}


