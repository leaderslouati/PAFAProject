using MediatR;
using Microsoft.Extensions.Logging;
using PAFA.Domain.Enums;
using PAFA.Domain.Interfaces;
using PAFA.Domain.IRepository;
using PAFA.Extraction.Commands.Pipeline;
using PAFA.Extraction.Validations;

namespace PAFA.Extraction.Handlers.Pipeline;

/// <summary>
/// Step 1 — Import files from SharePoint for the requested period.
///
/// For each file found in {Year}/{Month}:
///   - Validate folder structure (FOLD-001) — exclude if outside expected path
///   - Validate file name (NAME-001..004) — exclude if invalid, continue with others
///   - Upload to Blob Storage /inbound/{year}/{month}/filename.xlsx
///   - PATCH SharePoint ProcessingStatus = Processing (anti-duplicate lock)
/// </summary>
public sealed class ImportFilesHandler : IRequestHandler<ImportFilesCommand, ImportFilesResult>
{
    private readonly IRemoteFileSource _remoteSource;
    private readonly IBlobStorageService _blobService;
    private readonly IIngestionFileRepository _fileRepo;
    private readonly ILogger<ImportFilesHandler> _log;
    private readonly IFileSourceSettings _fileSettings;

    public ImportFilesHandler(
        IRemoteFileSource remoteSource,
        IBlobStorageService blobService,
        IIngestionFileRepository fileRepo,
        IFileSourceSettings fileSettings,
        ILogger<ImportFilesHandler> log)
    {
        _remoteSource = remoteSource;
        _blobService  = blobService;
        _fileRepo     = fileRepo;
        _fileSettings = fileSettings;
        _log          = log;
    }

    public async Task<ImportFilesResult> Handle(
        ImportFilesCommand cmd, CancellationToken ct)
    {
        var (year, month, correlationId) = cmd;
        var remotePath = $"{year}/{month:D2}";

        _log.LogInformation(
            "Pipeline step {Step} {Status} — CorrelationId: {CorrelationId}",
            "Import", "Starting", correlationId);

        if (!FolderPathValidator.HasValidYearMonthStructure(remotePath))
        {
            _log.LogWarning(
                "Folder path does not follow expected Year/Month structure: {Path} — CorrelationId: {CorrelationId}",
                remotePath, correlationId);
        }

        IReadOnlyList<RemoteFileEntry> remoteFiles;
        try
        {
            remoteFiles = await _remoteSource.ListFilesAsync(remotePath, "*.xlsx", ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex,
                "Failed to list SharePoint files at {Path} — CorrelationId: {CorrelationId}",
                remotePath, correlationId);
            return new ImportFilesResult(false, [],
                $"Failed to list SharePoint files: {ex.Message}");
        }

        _log.LogInformation(
            "{Count} file(s) found in SharePoint {Path} — CorrelationId: {CorrelationId}",
            remoteFiles.Count, remotePath, correlationId);

        // ── Anti-duplicate: load already-processed file names + dates ──────
        var alreadyLoaded = await _fileRepo.GetAlreadyLoadedFileNamesAsync(year, month, ct);
        var loadedDates   = await _fileRepo.GetLoadedFileModificationDatesAsync(year, month, ct);

        var importedFiles = new List<ImportedFile>();

        foreach (var remoteFile in remoteFiles)
        {
            // ── Folder validation ──────────────────────────────────────────
            if (!FolderPathValidator.IsValidYearMonthPath(remoteFile.FullRemotePath, year, month))
            {
                _log.LogWarning(
                    "Validation failed — CorrelationId: {CorrelationId} — File: {FileName} — Rules: FOLD-001 — Timestamp: {Timestamp}",
                    correlationId, remoteFile.FileName, DateTime.UtcNow);

                importedFiles.Add(new ImportedFile(
                    remoteFile.FileName,
                    string.Empty,
                    ImportStatus.SkippedInvalidFolder,
                    "File found outside the expected Year/Month folder structure"));
                continue;
            }

            // ── Anti-duplicate: skip files already processed with same date ─
            if (alreadyLoaded.Contains(remoteFile.FileName))
            {
                // Check if file was modified since last processing
                var isModified = loadedDates.TryGetValue(remoteFile.FileName, out var lastKnown)
                    && remoteFile.LastModified > lastKnown;

                if (!isModified)
                {
                    _log.LogInformation(
                        "Skipped (already processed, unchanged) — File: {FileName} — CorrelationId: {CorrelationId}",
                        remoteFile.FileName, correlationId);

                    importedFiles.Add(new ImportedFile(
                        remoteFile.FileName,
                        string.Empty,
                        ImportStatus.SkippedAlreadyProcessed,
                        "File already processed and unchanged since last ingestion"));
                    continue;
                }

                _log.LogInformation(
                    "File {FileName} was modified since last processing — re-importing. CorrelationId: {CorrelationId}",
                    remoteFile.FileName, correlationId);
            }

            // ── File name validation ───────────────────────────────────────
            var nameResult = FileNameValidator.Validate(
                remoteFile.FileName, _fileSettings.AllowedFilePrefixes, _fileSettings.AllowedExtensions);

            if (!nameResult.IsValid)
            {
                var failedRules = string.Join(", ", nameResult.Findings
                    .Where(f => f.Severity == "ERROR")
                    .Select(f => f.RuleId));

                var skipReason = string.Join("; ", nameResult.Findings
                    .Where(f => f.Severity == "ERROR")
                    .Select(f => f.Message));

                _log.LogWarning(
                    "Validation failed — CorrelationId: {CorrelationId} — File: {FileName} — Rules: {FailedRules} — Timestamp: {Timestamp}",
                    correlationId, remoteFile.FileName, failedRules, DateTime.UtcNow);

                importedFiles.Add(new ImportedFile(
                    remoteFile.FileName,
                    string.Empty,
                    ImportStatus.SkippedInvalidName,
                    skipReason));
                continue;
            }

            // ── Download + Upload to Blob /inbound/ ────────────────────────
            try
            {
                using var fileStream = await _remoteSource.DownloadFileAsync(
                    remoteFile.FullRemotePath, ct);

                var blobPath = await _blobService.UploadAsync(
                    remoteFile.FileName, fileStream,
                    container: "inbound",
                    year: year,
                    month: month,
                    ct: ct);

                // Anti-duplicate lock in SharePoint
                await _remoteSource.PatchStatusAsync(
                    remoteFile.FullRemotePath, "Processing", ct);

                importedFiles.Add(new ImportedFile(
                    remoteFile.FileName, blobPath, ImportStatus.Imported, null));

                _log.LogInformation(
                    "Pipeline step {Step} {Status} — File: {FileName} — BlobPath: {BlobPath} — CorrelationId: {CorrelationId}",
                    "Import", "FileImported",
                    remoteFile.FileName, blobPath, correlationId);
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "Failed to import file {FileName} — CorrelationId: {CorrelationId}",
                    remoteFile.FileName, correlationId);

                importedFiles.Add(new ImportedFile(
                    remoteFile.FileName, string.Empty, ImportStatus.SkippedInvalidName,
                    $"Import error: {ex.Message}"));
            }
        }

        var importedCount = importedFiles.Count(f => f.Status == ImportStatus.Imported);

        _log.LogInformation(
            "Pipeline step {Step} {Status} in {DurationMs}ms — Imported: {Imported}/{Total} — CorrelationId: {CorrelationId}",
            "Import", "Completed", 0, importedCount, importedFiles.Count, correlationId);

        return new ImportFilesResult(true, importedFiles);
    }
}
