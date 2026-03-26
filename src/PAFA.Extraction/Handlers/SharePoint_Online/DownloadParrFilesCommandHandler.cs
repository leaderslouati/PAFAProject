using MediatR;
using Microsoft.Extensions.Logging;
using PAFA.Domain.Interfaces;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Import;
using PAFA.Extraction.Commands.SharePoint;

namespace PAFA.Extraction.Commands.Sftp;

/// <summary>
/// Flux linéaire tout-en-un (CronJob / batch) :
///   SharePoint Download → Blob Upload → Parse → Validate → Insert DB
///
/// Structure SharePoint : {BaseInboundPath}/{Année}/{Mois}/
///   Ex: /2025/07/MOD520A_Jul25.xlsx
///
/// La période est déterminée UNIQUEMENT par le chemin du dossier SharePoint ({Année}/{Mois}).
/// Le nom du fichier n'est jamais utilisé pour déduire la période.
/// </summary>
public sealed class DownloadParrFilesCommandHandler
    : IRequestHandler<DownloadParrFilesCommand, DownloadParrFilesResult>
{
    private readonly IRemoteFileSource _fileSource;
    private readonly IBlobStorageService _blob;
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _uow;
    private readonly IFileSourceSettings _settings;
    private readonly ILogger<DownloadParrFilesCommandHandler> _log;

    public DownloadParrFilesCommandHandler(
        IRemoteFileSource fileSource,
        IBlobStorageService blob,
        IMediator mediator,
        IUnitOfWork uow,
        IFileSourceSettings settings,
        ILogger<DownloadParrFilesCommandHandler> log)
    {
        _fileSource = fileSource;
        _blob       = blob;
        _mediator   = mediator;
        _uow        = uow;
        _settings   = settings;
        _log        = log;
    }

    public async Task<DownloadParrFilesResult> Handle(
        DownloadParrFilesCommand cmd, CancellationToken ct)
    {
        // ── Période = dossier SharePoint /{year}/{month:D2}/ ─────
        // Source de vérité unique : le chemin du dossier.
        // cmd.Year/Month fournis par le CronJob ou l'appel API.
        // Fallback : mois courant UTC.
        var now         = DateTime.UtcNow;
        var periodYear  = cmd.Year  ?? now.Year;
        var periodMonth = cmd.Month ?? now.Month;

        var inboundPath = BuildInboundPath(periodYear, periodMonth);

        _log.LogInformation("═══ SharePoint Ingestion started ═══");
        _log.LogInformation("Période : {Year}-{Month:D2} → dossier source : {Path}",
            periodYear, periodMonth, inboundPath);

        var imported = new List<string>();
        var errors   = new List<FileError>();

        // ── 1. Test connexion ────────────────────────────────────
        var connected = await _fileSource.TestConnectionAsync(ct);
        if (!connected)
        {
            _log.LogError("Connexion SharePoint impossible");
            return new DownloadParrFilesResult(false, 0, 0, 0,
                imported, [new("FileSource", "Connection failed")]);
        }

        // ── 2. Lister tous les fichiers dans /{year}/{month:D2}/ ─
        var files = await _fileSource.ListFilesAsync(
            inboundPath, _settings.FilePattern, ct);

        if (cmd.FileNameFilter?.Any() == true)
            files = files.Where(f => cmd.FileNameFilter.Contains(f.FileName)).ToList();

        _log.LogInformation("{Count} fichier(s) à traiter dans {Path}", files.Count, inboundPath);

        if (!files.Any())
        {
            _log.LogInformation("Aucun fichier trouvé dans {Path}.", inboundPath);
            return new DownloadParrFilesResult(true, 0, 0, 0, imported, errors);
        }

        // ── 3. Pour chaque fichier : Download → Blob → Parse → Validate → Insert ──
        foreach (var file in files)
        {
            if (ct.IsCancellationRequested) break;

            _log.LogInformation("Traitement : {File} ({Size:N0} bytes)",
                file.FileName, file.SizeBytes);

            try
            {
                // 3a. Téléchargement en mémoire
                var bytes = await _fileSource.DownloadFileAsync(file.FullRemotePath, ct);
                _log.LogInformation("Téléchargé {Size:N0} bytes", bytes.Length);

                // 3b. Copie immédiate vers Blob (landing zone) avant tout traitement
                var blobPath = await _blob.UploadAsync(
                    file.FileName, bytes, "landing-zone", ct);
                _log.LogInformation("📦 Sauvegardé en blob : {BlobPath}", blobPath);

                // 3c. Parse → Validate → Insert
                // Période = dossier SharePoint uniquement (periodYear / periodMonth)
                var importResult = await _mediator.Send(new UploadParrFilesCommand(
                    FileName:     file.FileName,
                    FileContent:  bytes,
                    PeriodYear:   periodYear,
                    PeriodMonth:  periodMonth,
                    UploadedBy:   "SHAREPOINT_AUTO",
                    SourceSystem: DetectSourceSystem(file.FileName),
                    BlobPath:     blobPath), ct);

                if (importResult.Success)
                {
                    _log.LogInformation("✅ {File} — {Valid} lignes valides, {Read} lues",
                        file.FileName, importResult.RowsValid, importResult.RowsRead);

                    // 3d. Archivage → /Processed/{year}/{month:D2}/fichier.xlsx
                    var processedPath = BuildProcessedPath(periodYear, periodMonth, file.FileName);
                    await _fileSource.MoveFileAsync(file.FullRemotePath, processedPath, ct);
                    _log.LogInformation("📁 Archivé : {Path}", processedPath);
                    imported.Add(file.FileName);
                }
                else
                {
                    _log.LogWarning("❌ {File} — {Error}",
                        file.FileName, importResult.ErrorMessage);
                    errors.Add(new FileError(file.FileName,
                        importResult.ErrorMessage ?? "Import failed"));

                    // 3d. Archivage → /Failed/{year}/{month:D2}/fichier.xlsx
                    await SafeMoveFailed(file.FullRemotePath, file.FileName,
                        periodYear, periodMonth, ct);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Erreur lors du traitement de {File}", file.FileName);
                errors.Add(new FileError(file.FileName, ex.Message));
                await SafeMoveFailed(file.FullRemotePath, file.FileName,
                    periodYear, periodMonth, ct);
            }
        }

        _log.LogInformation(
            "═══ Ingestion terminée — {Ok} importé(s), {Ko} en erreur ═══",
            imported.Count, errors.Count);

        return new DownloadParrFilesResult(
            Success:         imported.Any() || !errors.Any(),
            FilesDownloaded: files.Count,
            FilesImported:   imported.Count,
            FilesFailed:     errors.Count,
            ImportedFiles:   imported,
            Errors:          errors);
    }

    // ════════════════════════════════════════════════════════════════
    //  Construction des chemins SharePoint
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Chemin source  : {BaseInboundPath}/{year}/{month:D2}
    /// Ex: "" → "/2025/07"   |   "/PARR" → "/PARR/2025/07"
    /// </summary>
    private string BuildInboundPath(int year, int month)
        => $"{_settings.BaseInboundPath.TrimEnd('/')}/{year}/{month:D2}";

    /// <summary>
    /// Chemin archivage succès : {ProcessedPath}/{year}/{month:D2}/{fileName}
    /// Ex: "/Processed/2025/07/MOD520A_Jul25.xlsx"
    /// </summary>
    private string BuildProcessedPath(int year, int month, string fileName)
        => $"{_settings.ProcessedPath.TrimEnd('/')}/{year}/{month:D2}/{fileName}";

    /// <summary>
    /// Chemin archivage erreur : {FailedPath}/{year}/{month:D2}/{fileName}
    /// Ex: "/Failed/2025/07/MOD520A_Jul25.xlsx"
    /// </summary>
    private string BuildFailedPath(int year, int month, string fileName)
        => $"{_settings.FailedPath.TrimEnd('/')}/{year}/{month:D2}/{fileName}";

    // ════════════════════════════════════════════════════════════════
    //  Helpers
    // ════════════════════════════════════════════════════════════════

    private async Task SafeMoveFailed(
        string remotePath, string fileName, int year, int month, CancellationToken ct)
    {
        try
        {
            var failed = BuildFailedPath(year, month, fileName);
            await _fileSource.MoveFileAsync(remotePath, failed, ct);
            _log.LogInformation("📁 Archivé dans Failed : {Path}", failed);
        }
        catch (Exception moveEx)
        {
            _log.LogError(moveEx, "Impossible de déplacer {File} vers /Failed", fileName);
        }
    }

    private static string DetectSourceSystem(string fileName)
    {
        var upper = fileName.ToUpperInvariant();
        if (upper.Contains("MOD520A") || upper.Contains("RPT_1364") ||
            upper.Contains("MOD700") || upper.Contains("EUC09"))
            return "CDSP";
        if (upper.Contains("TRANSFER") || upper.Contains("CLASS4AQ"))
            return "DDP";
        return "CDSP";
    }
}