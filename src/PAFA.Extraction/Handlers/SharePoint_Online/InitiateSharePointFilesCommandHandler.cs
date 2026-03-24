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
/// Flux :
///   1. Connexion SharePoint + liste des fichiers du dossier période
///   2. Suppression des fichiers déjà traités avec succès (Status = Loaded)
///   3. Validation du nom (NAME-001..004)
///   4. Téléchargement + upload dans MinIO (landing-zone)
///   5. Création IngestionJob + IngestionFile en base (Status = Validating)
///   6. Retour des FileId pour que le client enchaîne /parse ? /validate ? /persist
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
        _blob = blob;
        _uow = uow;
        _settings = settings;
        _helper = helper;
        _log = log;
    }

    public async Task<InitiateSharePointFilesResult> Handle(
        InitiateSharePointFilesCommand cmd, CancellationToken ct)
    {
        var (year, month) = (cmd.Year, cmd.Month);
        _log.LogInformation("[SP_INITIATE] Démarrage — Période: {Year}-{Month:D2}", year, month);

        // ?? 1. Test connexion SharePoint ??????????????????????????????????
        if (!await _fileSource.TestConnectionAsync(ct))
        {
            _log.LogError("[SP_INITIATE] Connexion SharePoint impossible.");
            return Fail(year, month, "Connexion SharePoint impossible.");
        }

        // ?? 2. Construire le chemin inbound + lister les fichiers ?????????
        var inboundPath = _helper.BuildInboundPath(year, month);
        _log.LogInformation("[SP_INITIATE] Dossier source : {Path}", inboundPath);

        var files = await _fileSource.ListFilesAsync(inboundPath, _settings.FilePattern, ct);

        if (cmd.FileNameFilter?.Count > 0)
            files = files.Where(f => cmd.FileNameFilter.Contains(f.FileName)).ToList();

        _log.LogInformation("[SP_INITIATE] {Count} fichier(s) trouvé(s) sur SharePoint", files.Count);

        if (files.Count == 0)
        {
            _log.LogInformation("[SP_INITIATE] Aucun fichier dans {Path}.", inboundPath);
            return new InitiateSharePointFilesResult(
                Success: true, Year: year, Month: month,
                PendingFiles: [], SkippedFiles: []);
        }

        // ?? 3. Charger les fichiers déjà traités avec succès ?????????????
        var alreadyLoaded = await _uow.IngestionFiles
            .GetAlreadyLoadedFileNamesAsync(year, month, ct);

        if (alreadyLoaded.Count > 0)
            _log.LogInformation(
                "[SP_INITIATE] {Count} fichier(s) déjà traité(s) seront ignorés : {Files}",
                alreadyLoaded.Count, string.Join(", ", alreadyLoaded));

        var pending = new List<PendingFileEntry>();
        var skipped = new List<SkippedFileRecord>();
        var now = DateTime.UtcNow;

        foreach (var file in files)
        {
            if (ct.IsCancellationRequested) break;

            // ?? Skip fichiers déjà chargés ????????????????????????????????
            if (alreadyLoaded.Contains(file.FileName))
            {
                _log.LogInformation("[SP_INITIATE] Skip (déjà Loaded) : {File}", file.FileName);
                skipped.Add(new SkippedFileRecord(file.FileName, "ALREADY_LOADED",
                    "Fichier déjà traité avec succès pour cette période.", now));
                continue;
            }

            // ?? Validation du nom (NAME-001..004) ?????????????????????????
            var nameValidation = FileNameValidator.Validate(
                file.FileName, _settings.AllowedFilePrefixes, _settings.AllowedExtensions);

            if (!nameValidation.IsValid)
            {
                foreach (var finding in nameValidation.Findings.Where(f => f.Severity == "ERROR"))
                {
                    _log.LogWarning("[SP_INITIATE] [{RuleId}] Skip — {File} : {Msg}",
                        finding.RuleId, file.FileName, finding.Message);
                    skipped.Add(new SkippedFileRecord(file.FileName, finding.RuleId, finding.Message, now));
                }
                await _helper.SafeMoveFailedAsync(file.FullRemotePath, file.FileName, year, month, ct);
                continue;
            }

            try
            {
                // ?? 4. Download SharePoint ? Upload MinIO ?????????????????
                _log.LogInformation("[SP_INITIATE] Transfert vers MinIO : {File}", file.FileName);

                string blobPath;
                using (var stream = await _fileSource.DownloadFileAsync(file.FullRemotePath, ct))
                {
                    blobPath = await _blob.UploadAsync(
                        file.FileName, stream, "landing-zone", year, month, ct);
                }

                _log.LogInformation("[SP_INITIATE] Blob stocké : {BlobPath}", blobPath);

                // ?? 5. Créer IngestionJob + IngestionFile en base ?????????
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
                    JobId: job.Id,
                    FileId: ingestionFile.Id,
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
                skipped.Add(new SkippedFileRecord(file.FileName, "TRANSFER_ERROR", ex.Message, now));
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

    // ?? Helper ????????????????????????????????????????????????????????????????

    private static InitiateSharePointFilesResult Fail(int year, int month, string error)
        => new(Success: false, Year: year, Month: month,
               PendingFiles: [], SkippedFiles: [], ErrorMessage: error);
}
