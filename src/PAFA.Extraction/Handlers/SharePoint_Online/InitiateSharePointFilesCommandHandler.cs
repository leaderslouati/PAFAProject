using MediatR;
using Microsoft.Extensions.Logging;
using PAFA.Domain.Entities;
using PAFA.Domain.Enums;
using PAFA.Domain.Interfaces;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.SharePoint;
using PAFA.Extraction.Helpers;
using PAFA.Extraction.Validations;

namespace PAFA.Extraction.Handlers.SharePoint_Online;

/// <summary>
/// Handler pour <see cref="InitiateSharePointFilesCommand"/>.
///
/// Flux de découverte :
///   1. Test connexion SharePoint
///   2. Lister /{BaseInboundPath}/{YYYY}/{MM}/ → fichiers neufs
///   3. Si ReprocessFailed=true :
///        - Lister /Processed/{YYYY}/{MM}/ → déplacer chaque fichier vers /inbound
///        - Lister /Failed/{YYYY}/{MM}/    → déplacer chaque fichier vers /inbound
///   4. Pour chaque fichier candidat : validation nom, download → MinIO, création Job+File
///
/// Year et Month sont optionnels : si null, utilise le mois/année courant du système.
/// </summary>
public sealed class InitiateSharePointFilesCommandHandler
    : IRequestHandler<InitiateSharePointFilesCommand, InitiateSharePointFilesResult>
{
    private readonly IRemoteFileSource _fileSource;
    private readonly IBlobStorageService _blob;
    private readonly IUnitOfWork _uow;
    private readonly IFileSourceSettings _settings;
    private readonly ISharePointFileHelper _helper;
    private readonly ILogger<InitiateSharePointFilesCommandHandler> _log;

    public InitiateSharePointFilesCommandHandler(
        IRemoteFileSource fileSource,
        IBlobStorageService blob,
        IUnitOfWork uow,
        IFileSourceSettings settings,
        ISharePointFileHelper helper,
        ILogger<InitiateSharePointFilesCommandHandler> log)
    {
        _fileSource = fileSource;
        _blob       = blob;
        _uow        = uow;
        _settings   = settings;
        _helper     = helper;
        _log        = log;
    }

    public async Task<InitiateSharePointFilesResult> Handle(
        InitiateSharePointFilesCommand cmd, CancellationToken ct)
    {
        // Résolution période : mois/année courant si non fourni
        var now   = DateTime.UtcNow;
        var year  = cmd.Year  ?? now.Year;
        var month = cmd.Month ?? now.Month;

        _log.LogInformation("[SP_INITIATE] Démarrage — Période: {Year}-{Month:D2} | ReprocessFailed: {Reprocess}",
            year, month, cmd.ReprocessFailed);

        // ── 1. Test connexion SharePoint ─────────────────────────────────
        if (!await _fileSource.TestConnectionAsync(ct))
        {
            _log.LogError("[SP_INITIATE] Connexion SharePoint impossible.");
            return Fail(year, month, "Connexion SharePoint impossible.");
        }

        // ── 2. Lister les fichiers neufs dans /inbound/{YYYY}/{MM}/ ──────
        var inboundPath = _helper.BuildInboundPath(year, month);
        _log.LogInformation("[SP_INITIATE] Dossier inbound : {Path}", inboundPath);

        var candidates = (await _fileSource.ListFilesAsync(inboundPath, _settings.FilePattern, ct))
            .ToList();

        _log.LogInformation("[SP_INITIATE] {Count} fichier(s) trouvé(s) dans /inbound", candidates.Count);

        // ── 3. Si reprocessFailed : déplacer /processed + /failed → /inbound ─
        if (cmd.ReprocessFailed)
        {
            var reprocessed = await CollectAndMoveToInboundAsync(year, month, ct);
            if (reprocessed.Count > 0)
            {
                _log.LogInformation("[SP_INITIATE] {Count} fichier(s) déplacé(s) vers /inbound pour retraitement : {Files}",
                    reprocessed.Count, string.Join(", ", reprocessed.Select(r => r.FileName)));
                candidates.AddRange(reprocessed);
            }
        }

        // ── 4. Filtre optionnel sur les noms ─────────────────────────────
        if (cmd.FileNameFilter?.Count > 0)
            candidates = candidates.Where(f => cmd.FileNameFilter.Contains(f.FileName)).ToList();

        if (candidates.Count == 0)
        {
            _log.LogInformation("[SP_INITIATE] Aucun fichier candidat pour {Year}-{Month:D2}.", year, month);
            return new InitiateSharePointFilesResult(
                Success: true, Year: year, Month: month,
                PendingFiles: [], SkippedFiles: []);
        }

        // ── 5. Charger les fichiers déjà traités avec succès ─────────────
        var alreadyLoaded = await _uow.IngestionFiles
            .GetAlreadyLoadedFileNamesAsync(year, month, ct);

        if (alreadyLoaded.Count > 0)
            _log.LogInformation(
                "[SP_INITIATE] {Count} fichier(s) déjà traités (ignorés) : {Files}",
                alreadyLoaded.Count, string.Join(", ", alreadyLoaded));

        var pending = new List<PendingFileEntry>();
        var skipped = new List<SkippedFileRecord>();
        var nowTs   = DateTime.UtcNow;

        foreach (var file in candidates)
        {
            if (ct.IsCancellationRequested) break;

            // Skip fichiers déjà traités avec succès
            if (alreadyLoaded.Contains(file.FileName))
            {
                _log.LogInformation("[SP_INITIATE] Skip (déjà Loaded) : {File}", file.FileName);
                skipped.Add(new SkippedFileRecord(file.FileName, "ALREADY_LOADED",
                    "Fichier déjà traité avec succès pour cette période.", nowTs));
                continue;
            }

            // Validation du nom (NAME-001..004)
            var nameValidation = FileNameValidator.Validate(
                file.FileName, _settings.AllowedFilePrefixes, _settings.AllowedExtensions);

            if (!nameValidation.IsValid)
            {
                foreach (var finding in nameValidation.Findings.Where(f => f.Severity == "ERROR"))
                {
                    _log.LogWarning("[SP_INITIATE] [{RuleId}] Skip — {File} : {Msg}",
                        finding.RuleId, file.FileName, finding.Message);
                    skipped.Add(new SkippedFileRecord(file.FileName, finding.RuleId, finding.Message, nowTs));
                }
                await _helper.SafeMoveFailedAsync(file.FullRemotePath, file.FileName, year, month, ct);
                continue;
            }

            try
            {
                // ── Download SharePoint → Upload MinIO ───────────────────
                _log.LogInformation("[SP_INITIATE] Transfert vers MinIO : {File}", file.FileName);

                string blobPath;
                using (var stream = await _fileSource.DownloadFileAsync(file.FullRemotePath, ct))
                {
                    blobPath = await _blob.UploadAsync(
                        file.FileName, stream, "landing-zone", year, month, ct);
                }

                _log.LogInformation("[SP_INITIATE] Blob stocké : {BlobPath}", blobPath);

                // ── Créer IngestionJob + IngestionFile en base ───────────
                var job = new IngestionJob
                {
                    JobName         = $"MANUAL_{year}_{month:D2}",
                    ReportingPeriod = new DateOnly(year, month, 1),
                    Status          = IngestionJobStatus.Processing,
                    FilesExpected   = 1,
                    StartedAt       = DateTime.UtcNow,
                    TriggeredBy     = JobTrigger.Manual
                };
                await _uow.IngestionJobs.AddAsync(job, ct);

                var ingestionFile = new IngestionFile
                {
                    IngestionJobId   = job.Id,
                    FileName         = file.FileName,
                    BlobPath         = blobPath,
                    Status           = IngestionFileStatus.Downloaded,
                    ValidationStatus = ValidationStatus.Pending,
                    DownloadedAt     = DateTime.UtcNow,
                    FileSizeBytes    = file.SizeBytes
                };
                await _uow.IngestionFiles.AddAsync(ingestionFile, ct);
                await _uow.SaveChangesAsync(ct);

                pending.Add(new PendingFileEntry(
                    JobId:    job.Id,
                    FileId:   ingestionFile.Id,
                    FileName: file.FileName,
                    BlobPath: blobPath,
                    SizeBytes: file.SizeBytes));

                _log.LogInformation(
                    "[SP_INITIATE] Prêt — {File} | FileId={FileId} | JobId={JobId}",
                    file.FileName, ingestionFile.Id, job.Id);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[SP_INITIATE] Erreur sur {File}", file.FileName);
                skipped.Add(new SkippedFileRecord(file.FileName, "TRANSFER_ERROR", ex.Message, nowTs));
            }
        }

        _log.LogInformation(
            "[SP_INITIATE] Terminé — {Pending} en attente, {Skipped} ignorés",
            pending.Count, skipped.Count);

        return new InitiateSharePointFilesResult(
            Success: true,
            Year: year,
            Month: month,
            PendingFiles: pending,
            SkippedFiles: skipped);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Liste les fichiers dans /processed/{YYYY}/{MM} et /failed/{YYYY}/{MM},
    /// puis les déplace chacun vers /inbound/{YYYY}/{MM} via SharePoint MoveFileAsync.
    /// Retourne les RemoteFileEntry mises à jour (avec le nouveau chemin /inbound).
    /// </summary>
    private async Task<List<RemoteFileEntry>> CollectAndMoveToInboundAsync(
        int year, int month, CancellationToken ct)
    {
        var result = new List<RemoteFileEntry>();

        var folders = new[]
        {
            _helper.BuildProcessedFolderPath(year, month),
            _helper.BuildFailedFolderPath(year, month)
        };

        foreach (var folder in folders)
        {
            List<RemoteFileEntry> files;
            try
            {
                files = (await _fileSource.ListFilesAsync(folder, _settings.FilePattern, ct)).ToList();
            }
            catch (Exception ex)
            {
                // Folder may not exist — not an error
                _log.LogDebug("[SP_INITIATE] Dossier inaccessible (peut ne pas exister) : {Folder} — {Err}",
                    folder, ex.Message);
                continue;
            }

            foreach (var file in files)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var newInboundPath = await _helper.MoveToInboundAsync(
                        file.FullRemotePath, file.FileName, year, month, ct);

                    result.Add(file with { FullRemotePath = newInboundPath });

                    _log.LogInformation(
                        "[SP_INITIATE] Déplacé vers /inbound : {File} (depuis {OldPath})",
                        file.FileName, file.FullRemotePath);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex,
                        "[SP_INITIATE] Impossible de déplacer {File} vers /inbound — ignoré",
                        file.FileName);
                }
            }
        }

        return result;
    }

    private static InitiateSharePointFilesResult Fail(int year, int month, string error)
        => new(Success: false, Year: year, Month: month,
               PendingFiles: [], SkippedFiles: [], ErrorMessage: error);
}

