using PAFA.Extraction.Commands.SharePoint;

namespace PAFA.Extraction.Helpers;

public interface ISharePointFileHelper
{
    string BuildInboundPath(int year, int month);
    string BuildProcessedPath(int year, int month, string fileName);
    string BuildFailedPath(int year, int month, string fileName);

    void LogFileSkipped(string fileName, string ruleId, string reason);

    Task SafeMoveFailedAsync(string remotePath, string fileName, int year, int month, CancellationToken ct);

    DownloadParrFilesResult CreateFailResult(string ruleId, string reason, string triggerSource, string triggerMode, DateTime now);
}