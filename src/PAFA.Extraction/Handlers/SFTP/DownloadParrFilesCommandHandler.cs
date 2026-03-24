using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PAFA.Domain.Interfaces;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Import;
using PAFA.Infrastructure.Sftp;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PAFA.Extraction.Commands.Sftp;

/// <summary>
/// Flux linéaire tout-en-un (CronJob / batch) :
///   SFTP Download → MinIO Upload → Parse → Validate → Insert DB
///
/// Le CronJob envoie la commande SANS période → le handler prend
/// TOUS les fichiers présents dans /upload et détecte la période
/// depuis le nom de chaque fichier (ex: "MOD520A_Feb25.xlsx" → 2025-02).
/// </summary>
public sealed partial class DownloadParrFilesCommandHandler
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
        _log.LogInformation("═══ SFTP Ingestion started ═══");
        if (cmd.Year.HasValue && cmd.Month.HasValue)
            _log.LogInformation("Forced period: {Year}-{Month:D2}", cmd.Year, cmd.Month);
        else
            _log.LogInformation("Period: auto-detect from filenames");

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

        // ── 2. List ALL available files in /upload ───────────────
        var files = await _sftp.ListFilesAsync(
            _settings.RemotePath, _settings.FilePattern, ct);

        if (cmd.FileNameFilter?.Any() == true)
            files = files.Where(f => cmd.FileNameFilter.Contains(f.FileName)).ToList();

        _log.LogInformation("{Count} file(s) to process", files.Count);

        if (!files.Any())
        {
            _log.LogInformation("No files found in {Path} — nothing to do.", _settings.RemotePath);
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
                // 3a. Detect period from filename (or use forced period)
                var (periodYear, periodMonth) = ResolvePeriodForFile(
                    file.FileName, cmd.Year, cmd.Month);
                _log.LogInformation("Period for {File}: {Year}-{Month:D2}",
                    file.FileName, periodYear, periodMonth);

                // 3b. Download from SFTP into memory
                var bytes = await _sftp.DownloadFileAsync(file.FullRemotePath, ct);
                _log.LogInformation("Downloaded {Size:N0} bytes from SFTP", bytes.Length);

                // 3c. Upload raw file to MinIO/Blob (landing zone)
                var blobPath = await _blob.UploadAsync(
                    file.FileName, bytes, "landing-zone", ct);
                _log.LogInformation("📦 Saved to blob: {BlobPath}", blobPath);

                // 3d. Parse → Validate → Insert (synchrone, même process)
                var importResult = await _mediator.Send(new UploadParrFilesCommand(
                    FileName: file.FileName,
                    FileContent: bytes,
                    PeriodYear: periodYear,
                    PeriodMonth: periodMonth,
                    UploadedBy: "SFTP_AUTO",
                    SourceSystem: DetectSourceSystem(file.FileName)), ct);

                if (importResult.Success)
                {
                    _log.LogInformation(
                        "✅ {File} — {Valid} rows, {Read} read",
                        file.FileName, importResult.RowsValid, importResult.RowsRead);

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

    // ════════════════════════════════════════════════════════════════
    //  Period detection from filename
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Resolves the reporting period for a file.
    /// Priority: 1. Forced (CLI args)  2. Parsed from filename  3. Current month
    ///
    /// Supports patterns like:
    ///   MOD520A_PAF_Reports_Feb25.xlsx        → 2025-02
    ///   RPT_1364_January_2025.xlsx             → 2025-01
    ///   PARR_2025_03_data.csv                  → 2025-03
    ///   EUC09_202502.xlsx                      → 2025-02
    /// </summary>
    static (int year, int month) ResolvePeriodForFile(
        string fileName, int? forcedYear, int? forcedMonth)
    {
        // Priority 1: Explicit period from CLI / env vars
        if (forcedYear.HasValue && forcedMonth.HasValue)
            return (forcedYear.Value, forcedMonth.Value);

        // Priority 2: Detect from filename
        var detected = DetectPeriodFromFileName(fileName);
        if (detected.HasValue)
            return detected.Value;

        // Priority 3: Fallback = current month
        var now = DateTime.UtcNow;
        return (now.Year, now.Month);
    }

    static (int year, int month)? DetectPeriodFromFileName(string fileName)
    {
        var upper = fileName.ToUpperInvariant();

        // Pattern 1: "Feb25", "January_2025", "Mar2025"
        // Match 3-letter or full month name followed by 2 or 4 digit year
        var monthNameMatch = MonthNamePattern().Match(upper);
        if (monthNameMatch.Success)
        {
            var monthStr = monthNameMatch.Groups["month"].Value;
            var yearStr = monthNameMatch.Groups["year"].Value;

            if (TryParseMonthName(monthStr, out int month))
            {
                int year = yearStr.Length == 2
                    ? 2000 + int.Parse(yearStr)
                    : int.Parse(yearStr);
                return (year, month);
            }
        }

        // Pattern 2: "YYYY_MM" or "YYYYMM" (e.g. "2025_03", "202502")
        var numericMatch = NumericDatePattern().Match(upper);
        if (numericMatch.Success)
        {
            int year = int.Parse(numericMatch.Groups["year"].Value);
            int month = int.Parse(numericMatch.Groups["month"].Value);
            if (year is >= 2020 and <= 2040 && month is >= 1 and <= 12)
                return (year, month);
        }

        return null;
    }

    static bool TryParseMonthName(string name, out int month)
    {
        // Try 3-letter abbreviation first, then full name
        var formats = new[] { "MMM", "MMMM" };
        foreach (var fmt in formats)
        {
            if (DateTime.TryParseExact(
                    name, fmt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dt))
            {
                month = dt.Month;
                return true;
            }
        }
        month = 0;
        return false;
    }

    [GeneratedRegex(@"(?<month>JAN(?:UARY)?|FEB(?:RUARY)?|MAR(?:CH)?|APR(?:IL)?|MAY|JUN(?:E)?|JUL(?:Y)?|AUG(?:UST)?|SEP(?:TEMBER)?|OCT(?:OBER)?|NOV(?:EMBER)?|DEC(?:EMBER)?)[_\-]?(?<year>\d{2,4})")]
    private static partial Regex MonthNamePattern();

    [GeneratedRegex(@"(?<year>20\d{2})[_\-]?(?<month>[01]\d)")]
    private static partial Regex NumericDatePattern();

    // ════════════════════════════════════════════════════════════════
    //  Helpers
    // ════════════════════════════════════════════════════════════════

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