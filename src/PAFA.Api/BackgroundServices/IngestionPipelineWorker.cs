using MediatR;
using Microsoft.AspNetCore.SignalR;
using PAFA.Api.Hubs;
using PAFA.Extraction.Commands.Import;
using PAFA.Extraction.Services;

namespace PAFA.Api.BackgroundServices;

/// <summary>
/// Background worker qui consomme la <see cref="IIngestionPipelineQueue"/>
/// et exécute les 4 étapes du pipeline pour chaque fichier reçu, en émettant
/// un événement SignalR <c>StepCompleted</c> après chaque étape.
///
/// ??? Étapes et événements SignalR ????????????????????????????????????????????
///   Step 1 — FileImport   : fichier déjà dans MinIO (fait par /start), notifié immédiatement
///   Step 2 — Parsing      : ParseFileCommand
///   Step 3 — Validation   : ValidateFileCommand
///   Step 4 — Persistence  : PersistFileCommand
///
/// Événements émis :
///   "PipelineStarted"  ? au début du traitement d'un fichier
///   "StepCompleted"    ? après chaque étape (succès ou échec)
///   "PipelineFinished" ? quand le fichier est entièrement traité
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

        // ?? Notification de démarrage du pipeline pour ce fichier ????????
        await hub.Clients.All.SendAsync("PipelineStarted", new PipelineStartedPayload(
            JobId: jobId,
            Year: 0, Month: 0,  // période non disponible ici, le front l'a déjà via /start
            TotalFiles: 1,
            FileIds: [fileId]), cancellationToken: ct);

        var succeeded = false;

        // ?? Chaque fichier = scope DI isolé ???????????????????????????????
        // DbContext + FilePipelineCache neufs ? pas de conflit entre fichiers
        using var scope = scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // ?? Step 1 : File Import (déjà fait par /start ? MinIO ready) ????
        // On notifie immédiatement avec la durée écoulée depuis l'enfilage
        var d1 = Elapsed(runStart);
        await NotifyStep(fileId, fileName, step: 1, stepName: "FileImport",
            success: true, durationMs: d1,
            details: new() { ["blobReady"] = true }, ct: ct);

        // ?? Step 2 : Parsing ??????????????????????????????????????????????
        var t2 = DateTime.UtcNow;
        var parseResult = await mediator.Send(new ParseFileCommand(fileId), ct);
        var d2 = Elapsed(t2);

        await NotifyStep(fileId, fileName, step: 2, stepName: "Parsing",
            success: parseResult.Success, durationMs: d2,
            error: parseResult.ErrorMessage,
            details: parseResult.Success
                ? new() { ["rowsRead"] = parseResult.TotalRows }
                : null,
            ct: ct);

        if (!parseResult.Success)
        {
            logger.LogWarning("[PIPELINE_WORKER] Parse échoué — {File} | {Err}", fileName, parseResult.ErrorMessage);
            await NotifyFinished(jobId, fileId, succeeded: false, runStart, ct);
            return;
        }

        // ?? Step 3 : Validation ???????????????????????????????????????????
        var t3 = DateTime.UtcNow;
        var validateResult = await mediator.Send(new ValidateFileCommand(fileId), ct);
        var d3 = Elapsed(t3);

        await NotifyStep(fileId, fileName, step: 3, stepName: "Validation",
            success: validateResult.Success, durationMs: d3,
            error: validateResult.ErrorMessage,
            details: validateResult.Success
                ? new()
                {
                    ["rowsValid"]         = validateResult.ValidRowCount,
                    ["rowsRejected"]      = validateResult.InvalidRowCount,
                    ["hasBlockingErrors"] = validateResult.HasBlockingErrors
                }
                : null,
            ct: ct);

        if (!validateResult.Success)
        {
            logger.LogWarning("[PIPELINE_WORKER] Validate échoué — {File} | {Err}", fileName, validateResult.ErrorMessage);
            await NotifyFinished(jobId, fileId, succeeded: false, runStart, ct);
            return;
        }

        // ?? Step 4 : Persistence ??????????????????????????????????????????
        var t4 = DateTime.UtcNow;
        var persistResult = await mediator.Send(new PersistFileCommand(fileId), ct);
        var d4 = Elapsed(t4);

        succeeded = persistResult.Success;

        await NotifyStep(fileId, fileName, step: 4, stepName: "Persistence",
            success: persistResult.Success, durationMs: d4,
            error: persistResult.ErrorMessage,
            details: persistResult.Success
                ? new()
                {
                    ["metricsInserted"] = persistResult.MetricsInserted,
                    ["blobPath"]        = persistResult.FinalBlobPath
                }
                : null,
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
            JobId:          jobId,
            TotalFiles:     1,
            Succeeded:      succeeded ? 1 : 0,
            Failed:         succeeded ? 0 : 1,
            TotalDurationMs: Elapsed(runStart)), ct);

    private static long Elapsed(DateTime since)
        => (long)(DateTime.UtcNow - since).TotalMilliseconds;
}


