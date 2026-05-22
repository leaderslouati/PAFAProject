using PAFA.Extraction.Commands.SharePoint;

namespace PAFA.Extraction.Helpers;

public interface ISharePointFileHelper
{
    string BuildInboundPath(int year, int month);
    string BuildProcessedPath(int year, int month, string fileName);
    string BuildFailedPath(int year, int month, string fileName);

    /// <summary>
    /// Builds the folder path used to list already-processed files for a period.
    /// e.g. /Processed/2025/07
    /// </summary>
    string BuildProcessedFolderPath(int year, int month);

    /// <summary>
    /// Builds the folder path used to list failed files for a period.
    /// e.g. /Failed/2025/07
    /// </summary>
    string BuildFailedFolderPath(int year, int month);

    void LogFileSkipped(string fileName, string ruleId, string reason);

    Task SafeMoveFailedAsync(string remotePath, string fileName, int year, int month, CancellationToken ct);

    /// <summary>
    /// Moves a file from /processed or /failed back to /inbound so it can be reprocessed.
    /// </summary>
    Task<string> MoveToInboundAsync(string remotePath, string fileName, int year, int month, CancellationToken ct);

    DownloadParrFilesResult CreateFailResult(string ruleId, string reason, string triggerSource, string triggerMode, DateTime now);
}