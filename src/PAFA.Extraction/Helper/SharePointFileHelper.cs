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

    // Processed and Failed are sub-folders of the period folder:
    //   {BaseInboundPath}/{YYYY}/{MM}/Processed/
    //   {BaseInboundPath}/{YYYY}/{MM}/Failed/

    public string BuildProcessedFolderPath(int year, int month)
        => $"{BuildInboundPath(year, month)}/Processed";

    public string BuildFailedFolderPath(int year, int month)
        => $"{BuildInboundPath(year, month)}/Failed";

    public string BuildProcessedPath(int year, int month, string fileName)
        => $"{BuildInboundPath(year, month)}/Processed/{fileName}";

    public string BuildFailedPath(int year, int month, string fileName)
        => $"{BuildInboundPath(year, month)}/Failed/{fileName}";

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
            _log.LogInformation("Archivé dans /Failed : {Path}", failedPath);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Impossible de déplacer {File} vers /Failed", fileName);
        }
    }

    public async Task<string> MoveToInboundAsync(string remotePath, string fileName, int year, int month, CancellationToken ct)
    {
        var inboundPath = $"{BuildInboundPath(year, month)}/{fileName}";
        await _fileSource.MoveFileAsync(remotePath, inboundPath, ct);
        _log.LogInformation("Déplacé vers /inbound : {OldPath} → {NewPath}", remotePath, inboundPath);
        return inboundPath;
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