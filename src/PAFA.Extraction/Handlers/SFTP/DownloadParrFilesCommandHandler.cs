using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PAFA.Domain.Entities;
using PAFA.Domain.Enums;
using PAFA.Domain.Interfaces;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Import;
using PAFA.Infrastructure.Sftp;

namespace PAFA.Extraction.Commands.Sftp;

/// <summary>
/// Flux linéaire tout-en-un (CronJob / batch) :
///   SFTP Download → MinIO Upload → Parse → Validate → Insert DB
/// Un seul IngestionJob en DB pour tout le batch.
/// Pas de RabbitMQ, pas de consumer séparé.
/// </summary>
public sealed class DownloadParrFilesCommandHandler
    : IRequestHandler<DownloadParrFilesCommand, DownloadParrFilesResult>
{
    private readonly ISftpFileSource _sftp;
    private readonly IBlobStorageService _blob;
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _uow;
    private readonly SftpSettings _settings;
    private readonly ILogger<DownloadParrFilesCommandHandler> _log;

    public DownloadParrFilesCommandHandler(
        ISftpFileSource sftp,
        IBlobStorageService blob,
        IMediator mediator,
        IUnitOfWork uow,
        IOptions<SftpSettings> settings,
        ILogger<DownloadParrFilesCommandHandler> log)
    {
        _sftp = sftp;
        _blob = blob;
        _mediator = mediator;
        _uow = uow;
        _settings = settings.Value;
        _log = log;
    }

    public async Task<DownloadParrFilesResult> Handle(
        DownloadParrFilesCommand cmd, CancellationToken ct)
    {
        _log.LogInformation(
            "═══ SFTP Ingestion started — period {Year}-{Month:D2} ═══",
            cmd.Year, cmd.Month);

        var imported = new List<string>();
        var errors = new List<FileError>();

        // ── 1. Test SFTP connection ──────────────────────────────
        var connected = await _sftp.TestConnectionAsync(ct);
        if (!connected)
        {
            _log.LogError("Cannot connect to SFTP {Host}:{Port}",
                _settings.Host, _settings.Port);
            return new DownloadParrFilesResult(false, 0, 0, 0,
                imported, [new("SFTP", "Connection failed")]);
        }

        // ── 2. List available files ──────────────────────────────
        var files = await _sftp.ListFilesAsync(
            _settings.RemotePath, _settings.FilePattern, ct);

        if (cmd.FileNameFilter?.Any() == true)
            files = files.Where(f => cmd.FileNameFilter.Contains(f.FileName)).ToList();

        _log.LogInformation("{Count} file(s) to process", files.Count);

        if (!files.Any())
        {
            _log.LogWarning("No files found in {Path}", _settings.RemotePath);
            return new DownloadParrFilesResult(true, 0, 0, 0, imported, errors);
        }

        // ── 3. For each file: Download → Blob → Parse → Validate → Insert ──
        foreach (var file in files)
        {
            if (ct.IsCancellationRequested) break;

            _log.LogInformation("Processing: {File} ({Size:N0} bytes)",
                file.FileName, file.SizeBytes);

            try
            {
                // 3a. Download from SFTP into memory
                var bytes = await _sftp.DownloadFileAsync(file.FullRemotePath, ct);
                _log.LogInformation("Downloaded {Size:N0} bytes from SFTP", bytes.Length);

                // 3b. Upload raw file to MinIO/Blob (landing zone)
                var blobPath = await _blob.UploadAsync(
                    file.FileName, bytes, "landing-zone", ct);
                _log.LogInformation("📦 Saved to blob: {BlobPath}", blobPath);

                // 3c. Parse → Validate → Insert (synchrone, même process)
                var importResult = await _mediator.Send(new UploadParrFilesCommand(
                    FileName: file.FileName,
                    FileContent: bytes,
                    PeriodYear: cmd.Year,
                    PeriodMonth: cmd.Month,
                    UploadedBy: "SFTP_AUTO",
                    SourceSystem: DetectSourceSystem(file.FileName)), ct);

                if (importResult.Success)
                {
                    _log.LogInformation(
                        "✅ {File} — {Valid} rows, {Read} read",
                        file.FileName, importResult.RowsValid, importResult.RowsRead);

                    // Move SFTP file → /processed
                    var processed = $"{_settings.ProcessedPath}/{file.FileName}";
                    await _sftp.MoveFileAsync(file.FullRemotePath, processed, ct);

                    imported.Add(file.FileName);
                }
                else
                {
                    _log.LogWarning("❌ {File} — {Error}",
                        file.FileName, importResult.ErrorMessage);
                    errors.Add(new FileError(file.FileName,
                        importResult.ErrorMessage ?? "Import failed"));

                    // Move SFTP file → /failed
                    await SafeMoveFailed(file.FullRemotePath, file.FileName, ct);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error processing {File}", file.FileName);
                errors.Add(new FileError(file.FileName, ex.Message));
                await SafeMoveFailed(file.FullRemotePath, file.FileName, ct);
            }
        }

        _log.LogInformation(
            "═══ SFTP Ingestion complete — {Ok} imported, {Ko} failed ═══",
            imported.Count, errors.Count);

        return new DownloadParrFilesResult(
            Success: imported.Any() || !errors.Any(),
            FilesDownloaded: files.Count,
            FilesImported: imported.Count,
            FilesFailed: errors.Count,
            ImportedFiles: imported,
            Errors: errors);
    }

    private async Task SafeMoveFailed(string remotePath, string fileName, CancellationToken ct)
    {
        try
        {
            var failed = $"{_settings.FailedPath}/{fileName}";
            await _sftp.MoveFileAsync(remotePath, failed, ct);
        }
        catch (Exception moveEx)
        {
            _log.LogError(moveEx, "Cannot move {File} to /failed", fileName);
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