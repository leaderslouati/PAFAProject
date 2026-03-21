namespace PAFA.Reports.Batch.Models;

/// <summary>
/// Result of a single report generation operation.
/// </summary>
public sealed record ReportGenerationResult
{
    public required string ReportName { get; init; }
    public required bool Success { get; init; }
    public string? FilePath { get; init; }
    public string? ErrorMessage { get; init; }
    public Exception? Exception { get; init; }
    public TimeSpan Duration { get; init; }
    public long FileSizeBytes { get; init; }

    public static ReportGenerationResult Successful(
        string reportName, string filePath, TimeSpan duration, long fileSize)
        => new()
        {
            ReportName = reportName,
            Success = true,
            FilePath = filePath,
            Duration = duration,
            FileSizeBytes = fileSize
        };

    public static ReportGenerationResult Failed(
        string reportName, string errorMessage, Exception? exception, TimeSpan duration)
        => new()
        {
            ReportName = reportName,
            Success = false,
            ErrorMessage = errorMessage,
            Exception = exception,
            Duration = duration
        };
}
