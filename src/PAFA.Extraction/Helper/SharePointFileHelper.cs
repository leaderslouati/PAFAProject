using Microsoft.Extensions.Logging;
using PAFA.Domain.Interfaces;
using PAFA.Extraction.Commands.SharePoint;

namespace PAFA.Extraction.Helpers;

public class SharePointFileHelper : ISharePointFileHelper
{
    private readonly IFileSourceSettings _settings;
    private readonly IRemoteFileSource _fileSource;
    private readonly ILogger<SharePointFileHelper> _log;

    public SharePointFileHelper(
        IFileSourceSettings settings,
        IRemoteFileSource fileSource,
        ILogger<SharePointFileHelper> log)
    {
        _settings = settings;
        _fileSource = fileSource;
        _log = log;
    }

    public string BuildInboundPath(int year, int month)
        => $"{_settings.BaseInboundPath.TrimEnd('/')}/{year}/{month:D2}";

    public string BuildProcessedPath(int year, int month, string fileName)
        => $"{_settings.ProcessedPath.TrimEnd('/')}/{year}/{month:D2}/{fileName}";

    public string BuildFailedPath(int year, int month, string fileName)
        => $"{_settings.FailedPath.TrimEnd('/')}/{year}/{month:D2}/{fileName}";

    public void LogFileSkipped(string fileName, string ruleId, string reason)
    {
        _log.LogWarning("[{RuleId}] File skipped — {FileName} | Reason: {Reason}", ruleId, fileName, reason);
    }

    public async Task SafeMoveFailedAsync(string remotePath, string fileName, int year, int month, CancellationToken ct)
    {
        try
        {
            var failedPath = BuildFailedPath(year, month, fileName);
            await _fileSource.MoveFileAsync(remotePath, failedPath, ct);
            _log.LogInformation("📁 Archivé dans Failed : {Path}", failedPath);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Impossible de déplacer {File} vers /Failed", fileName);
        }
    }

    public DownloadParrFilesResult CreateFailResult(string ruleId, string reason, string triggerSource, string triggerMode, DateTime now)
    {
        return new DownloadParrFilesResult(
            Success: false,
            FilesDownloaded: 0, FilesImported: 0, FilesFailed: 0,
            ImportedFiles: [], Errors: [],
            SkippedFiles: [new SkippedFileRecord("*", ruleId, reason, now)],
            TriggerSource: triggerSource,
            TriggerMode: triggerMode);
    }
}