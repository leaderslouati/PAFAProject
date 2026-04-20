using MediatR;
using Microsoft.Extensions.Logging;
using PAFA.Domain.Enums;
using PAFA.Domain.Interfaces;
using PAFA.Domain.IRepository;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Import;
using PAFA.Extraction.Validations;
using PAFA.Extraction.Helpers; 

namespace PAFA.Extraction.Commands.SharePoint;

public sealed class DownloadParrFilesCommandHandler
    : IRequestHandler<DownloadParrFilesCommand, DownloadParrFilesResult>
{
    private readonly IRemoteFileSource _fileSource;
    private readonly IBlobStorageService _blob;
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _uow;
    private readonly IFileSourceSettings _settings;
    private readonly IIngestionJobRepository _jobRepo;
    private readonly ILogger<DownloadParrFilesCommandHandler> _log;
    private readonly ISharePointFileHelper _helper; 

    public DownloadParrFilesCommandHandler(
        IRemoteFileSource fileSource,
        IBlobStorageService blob,
        IMediator mediator,
        IUnitOfWork uow,
        IFileSourceSettings settings,
        IIngestionJobRepository jobRepo,
        ILogger<DownloadParrFilesCommandHandler> log,
        ISharePointFileHelper helper)
    {
        _fileSource = fileSource;
        _blob = blob;
        _mediator = mediator;
        _uow = uow;
        _settings = settings;
        _jobRepo = jobRepo;
        _log = log;
        _helper = helper;
    }

    // ── Fenêtre cron : jours 18–21 du mois ───────────────────────────────
    private static bool IsWithinCronWindow(DateTime utcNow)
        => utcNow.Day is >= 18 and <= 21;

    public async Task<DownloadParrFilesResult> Handle(
        DownloadParrFilesCommand cmd, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var periodYear = cmd.Year ?? now.Year;
        var periodMonth = cmd.Month ?? now.Month;

        // ── Vérification fenêtre (uniquement pour les déclenchements automatiques) ──
        if (cmd.TriggerMode == TriggerMode.Automatic && !IsWithinCronWindow(now))
        {
            _log.LogWarning(
                "[CRON] Déclenchement automatique hors fenêtre (jour {Day}). Abandon. Period: {Year}-{Month:D2}",
                now.Day, periodYear, periodMonth);
            return new DownloadParrFilesResult(false, 0, 0, 0, [], [], [],
                cmd.TriggerSource, cmd.TriggerMode.ToString());
        }

        // ── Reprise intelligente : vérifier si le job est déjà completé ──
        if (await _jobRepo.IsAlreadyCompletedAsync(periodYear, periodMonth, ct))
        {
            _log.LogInformation(
                "[CRON] Job déjà complété pour {Year}-{Month:D2}. Aucun traitement nécessaire.",
                periodYear, periodMonth);
            return new DownloadParrFilesResult(true, 0, 0, 0, [], [], [],
                cmd.TriggerSource, cmd.TriggerMode.ToString());
        }

        // ── Reprise intelligente : charger les fichiers déjà traités ─────
        var alreadyLoadedFiles = await _uow.IngestionFiles
            .GetAlreadyLoadedFileNamesAsync(periodYear, periodMonth, ct);

        if (alreadyLoadedFiles.Count > 0)
            _log.LogInformation(
                "[CRON] Reprise — {Count} fichier(s) déjà chargé(s) seront ignorés : {Files}",
                alreadyLoadedFiles.Count, string.Join(", ", alreadyLoadedFiles));

        // ── Utilisation du Helper pour le chemin ──────────────────────────
        var inboundPath = _helper.BuildInboundPath(periodYear, periodMonth);

        // ── Log trigger context ───────────────────────────────────────────
        _log.LogInformation(
            "Trigger: {Mode} | Source: {Source} | Period: {Year}-{Month:D2}",
            cmd.TriggerMode, cmd.TriggerSource, periodYear, periodMonth);

        if (cmd.TriggerMode == TriggerMode.Manual)
            _log.LogWarning(
                "Manual trigger outside automatic window — Source: {Source} | Period: {Year}-{Month:D2}",
                cmd.TriggerSource, periodYear, periodMonth);

        // ── FOLD-002 ─────────────────────────────────────────────────────
        if (_settings.EnforceYearMonthFolderStructure
            && !FolderPathValidator.HasValidYearMonthStructure(inboundPath))
        {
            _log.LogError("[FOLD-002] Inbound path '{Path}' does not conform to Year/Month structure.", inboundPath);
            return _helper.CreateFailResult("FOLD-002", $"Inbound path '{inboundPath}' is not a valid Year/Month folder.",
                cmd.TriggerSource, cmd.TriggerMode.ToString(), now);
        }

        _log.LogInformation("═══ SharePoint Ingestion started ═══");
        _log.LogInformation("Période : {Year}-{Month:D2} → dossier source : {Path}", periodYear, periodMonth, inboundPath);

        var imported = new List<string>();
        var errors = new List<FileError>();
        var skipped = new List<SkippedFileRecord>();

        // ── 1. Test connexion ─────────────────────────────────────────────
        if (!await _fileSource.TestConnectionAsync(ct))
        {
            _log.LogError("Connexion SharePoint impossible");
            return new DownloadParrFilesResult(false, 0, 0, 0, imported,
                [new("FileSource", "Connection failed")], skipped,
                cmd.TriggerSource, cmd.TriggerMode.ToString());
        }

        // ── 2. Lister les fichiers ────────────────────────────────────────
        var files = await _fileSource.ListFilesAsync(inboundPath, _settings.FilePattern, ct);

        if (cmd.FileNameFilter?.Any() == true)
            files = files.Where(f => cmd.FileNameFilter.Contains(f.FileName)).ToList();

        _log.LogInformation("{Count} fichier(s) trouvé(s) dans {Path}", files.Count, inboundPath);

        if (!files.Any())
        {
            _log.LogInformation("Aucun fichier trouvé dans {Path}.", inboundPath);
            return new DownloadParrFilesResult(true, 0, 0, 0, imported, errors, skipped,
                cmd.TriggerSource, cmd.TriggerMode.ToString());
        }

        // ── 3. Pour chaque fichier ────────────────────────────────────────
        foreach (var file in files)
        {
            if (ct.IsCancellationRequested) break;

            // ── Reprise : ignorer les fichiers déjà chargés ───────────────
            if (alreadyLoadedFiles.Contains(file.FileName))
            {
                _log.LogInformation("[CRON] Skip (déjà chargé) : {File}", file.FileName);
                imported.Add(file.FileName);
                continue;
            }

            // FOLD-001
            if (_settings.EnforceYearMonthFolderStructure
                && !FolderPathValidator.IsValidYearMonthPath(file.FullRemotePath, periodYear, periodMonth))
            {
                _helper.LogFileSkipped(file.FileName, "FOLD-001",
                    $"File path '{file.FullRemotePath}' is outside expected folder '{inboundPath}'.");
                skipped.Add(new SkippedFileRecord(file.FileName, "FOLD-001",
                    $"File is outside the expected folder '{inboundPath}'.", now));

                await _helper.SafeMoveFailedAsync(file.FullRemotePath, file.FileName, periodYear, periodMonth, ct);
                continue;
            }

            // NAME-001 → NAME-004
            var nameValidation = FileNameValidator.Validate(
                file.FileName, _settings.AllowedFilePrefixes, _settings.AllowedExtensions);

            if (!nameValidation.IsValid)
            {
                foreach (var finding in nameValidation.Findings.Where(f => f.Severity == "ERROR"))
                {
                    _helper.LogFileSkipped(file.FileName, finding.RuleId, finding.Message);
                    skipped.Add(new SkippedFileRecord(file.FileName, finding.RuleId, finding.Message, now));
                }
                await _helper.SafeMoveFailedAsync(file.FullRemotePath, file.FileName, periodYear, periodMonth, ct);
                continue;
            }

            foreach (var w in nameValidation.Findings.Where(f => f.Severity == "WARNING"))
                _log.LogWarning("[{RuleId}] {File} — {Msg}", w.RuleId, file.FileName, w.Message);

            _log.LogInformation("Traitement : {File} ({Size:N0} bytes)", file.FileName, file.SizeBytes);

            try
            {
                string blobPath;

                // ── Transfert vers le Blob via Stream (Optimisation RAM) ──
                using (var fileStream = await _fileSource.DownloadFileAsync(file.FullRemotePath, ct))
                {
                    _log.LogInformation("Téléchargement et transfert en cours vers le Blob Storage...");
                    blobPath = await _blob.UploadAsync(file.FileName, fileStream, "landing-zone", periodYear, periodMonth, ct);
                }

                _log.LogInformation("📦 Sauvegardé en blob : {BlobPath}", blobPath);

                // Map TriggerMode → JobTrigger for the IngestionJob entity
                var jobTrigger = cmd.TriggerSource switch
                {
                    "CRON_AUTO" => JobTrigger.Scheduler,
                    "MANUAL_REPROCESS" => JobTrigger.Retry,
                    _ => JobTrigger.Api
                };

                // ── Reprocess: link to parent job ─────────────────────────
                Guid? parentJobId = null;
                int retryCount = 0;

                if (cmd.TriggerSource == "MANUAL_REPROCESS")
                {
                    var previousJob = await _jobRepo.GetLatestByPeriodAsync(periodYear, periodMonth, ct);
                    if (previousJob is not null)
                    {
                        parentJobId = previousJob.Id;
                        retryCount = previousJob.RetryCount + 1;
                        _log.LogInformation(
                            "AUDIT | Reprocess | ParentJobId={Parent} | RetryCount={Retry} | Period={Year}-{Month:D2}",
                            parentJobId, retryCount, periodYear, periodMonth);
                    }
                }

                // ── Envoi de la commande de traitement ────────────────────
                var importResult = await _mediator.Send(new UploadParrFilesCommand(
                    FileName: file.FileName,
                    // Note : FileContent (byte[]) a été supprimé de la commande
                    PeriodYear: periodYear,
                    PeriodMonth: periodMonth,
                    BlobPath: blobPath,
                    TriggerSource: cmd.TriggerSource,
                    ParentJobId: parentJobId,
                    RetryCount: retryCount,
                    JobTrigger: jobTrigger), ct);

                if (importResult.Success)
                {
                    _log.LogInformation("✅ {File} — {Valid} lignes valides, {Read} lues",
                        file.FileName, importResult.RowsValid, importResult.RowsRead);

                    var processedPath = _helper.BuildProcessedPath(periodYear, periodMonth, file.FileName);
                    await _fileSource.MoveFileAsync(file.FullRemotePath, processedPath, ct);

                    _log.LogInformation("📁 Archivé : {Path}", processedPath);
                    imported.Add(file.FileName);
                }
                else
                {
                    _log.LogWarning("❌ {File} — {Error}", file.FileName, importResult.ErrorMessage);
                    errors.Add(new FileError(file.FileName, importResult.ErrorMessage ?? "Import failed"));
                    await _helper.SafeMoveFailedAsync(file.FullRemotePath, file.FileName, periodYear, periodMonth, ct);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Erreur lors du traitement de {File}", file.FileName);
                errors.Add(new FileError(file.FileName, ex.Message));
                await _helper.SafeMoveFailedAsync(file.FullRemotePath, file.FileName, periodYear, periodMonth, ct);
            }
        }

        _log.LogInformation(
            "═══ Ingestion terminée — {Ok} importé(s), {Ko} en erreur, {Skip} ignoré(s) ═══",
            imported.Count, errors.Count, skipped.Count);

        return new DownloadParrFilesResult(
            Success: imported.Any() || (!errors.Any() && !skipped.Any()),
            FilesDownloaded: files.Count,
            FilesImported: imported.Count,
            FilesFailed: errors.Count,
            ImportedFiles: imported,
            Errors: errors,
            SkippedFiles: skipped,
            TriggerSource: cmd.TriggerSource,
            TriggerMode: cmd.TriggerMode.ToString());
    }
}