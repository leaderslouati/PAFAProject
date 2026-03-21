using Microsoft.Extensions.Logging;
using PAFA.Reports.Batch.Models;

namespace PAFA.Reports.Batch.Core;

/// <summary>
/// Abstract base class implementing the Template Method pattern.
/// Handles the robust .tmp ? rename workflow to prevent file corruption.
/// 
/// ANTI-CRASH ARCHITECTURE (from ipc-back):
/// 1. Generate content to a temporary file (.tmp)
/// 2. If successful, atomically rename to final name
/// 3. If error, delete .tmp and log (don't crash)
/// </summary>
public abstract class ReportGenerator
{
    protected readonly ILogger Logger;

    protected ReportGenerator(ILogger logger)
    {
        Logger = logger;
    }

    /// <summary>
    /// Template Method: Orchestrates the entire generation workflow.
    /// SEALED to enforce the robust pattern.
    /// </summary>
    public async Task<ReportGenerationResult> GenerateAsync(
        ReportGenerationContext context, 
        CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        var reportName = GetReportName(context);
        var finalPath = GetFinalFilePath(context);
        var tempPath = $"{finalPath}.tmp";

        try
        {
            Logger.LogInformation(
                "[{ReportType}] Starting generation: {ReportName}", 
                GetType().Name, reportName);

            // Step 1: Ensure directories exist
            EnsureDirectoriesExist(context);

            // Step 2: Validate context (hook for subclasses)
            await ValidateContextAsync(context, ct);

            // Step 3: Generate to temporary file
            await GenerateToTempFileAsync(context, tempPath, ct);

            // Step 4: Validate generated file
            ValidateTempFile(tempPath);

            // Step 5: Atomic rename
            PerformAtomicRename(tempPath, finalPath);

            var duration = DateTime.UtcNow - startTime;
            var fileSize = new FileInfo(finalPath).Length;

            Logger.LogInformation(
                "[{ReportType}] ? Successfully generated: {FileName} ({Size:N0} bytes, {Duration:F2}s)",
                GetType().Name, Path.GetFileName(finalPath), fileSize, duration.TotalSeconds);

            return ReportGenerationResult.Successful(reportName, finalPath, duration, fileSize);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;

            Logger.LogError(ex,
                "[{ReportType}] ? Failed to generate: {ReportName}. Error: {ErrorMessage}",
                GetType().Name, reportName, ex.Message);

            // Clean up temporary file if it exists
            CleanupTempFile(tempPath);

            return ReportGenerationResult.Failed(reportName, ex.Message, ex, duration);
        }
    }

    // ???????????????????????????????????????????????????????????????????????
    //  ABSTRACT METHODS (must be implemented by subclasses)
    // ???????????????????????????????????????????????????????????????????????

    /// <summary>
    /// Returns the file name (without path) for the report.
    /// Example: "PAFA_Report_2025_01_PDF.pdf"
    /// </summary>
    protected abstract string GetFileName(ReportGenerationContext context);

    /// <summary>
    /// Returns the file extension (e.g., ".pdf", ".xlsx").
    /// </summary>
    protected abstract string GetFileExtension();

    /// <summary>
    /// Core generation logic: write content to the provided stream.
    /// The stream is already opened and will be closed by the base class.
    /// </summary>
    protected abstract Task GenerateContentAsync(
        ReportGenerationContext context, 
        Stream stream, 
        CancellationToken ct);

    // ???????????????????????????????????????????????????????????????????????
    //  VIRTUAL METHODS (can be overridden by subclasses if needed)
    // ???????????????????????????????????????????????????????????????????????

    /// <summary>
    /// Optional pre-generation validation (e.g., check data availability).
    /// </summary>
    protected virtual Task ValidateContextAsync(
        ReportGenerationContext context, 
        CancellationToken ct)
        => Task.CompletedTask;

    /// <summary>
    /// Optional post-generation validation (e.g., check file size, format).
    /// </summary>
    protected virtual void ValidateTempFile(string tempPath)
    {
        var fileInfo = new FileInfo(tempPath);
        if (!fileInfo.Exists)
            throw new InvalidOperationException($"Temporary file was not created: {tempPath}");
        
        if (fileInfo.Length == 0)
            throw new InvalidOperationException($"Generated file is empty: {tempPath}");
    }

    // ???????????????????????????????????????????????????????????????????????
    //  PRIVATE HELPER METHODS (Template Method implementation details)
    // ???????????????????????????????????????????????????????????????????????

    private string GetReportName(ReportGenerationContext context)
        => $"{GetFileName(context)} ({context.GetContextDescription()})";

    private string GetFinalFilePath(ReportGenerationContext context)
        => Path.Combine(context.OutputDirectory, GetFileName(context));

    private void EnsureDirectoriesExist(ReportGenerationContext context)
    {
        Directory.CreateDirectory(context.OutputDirectory);
        Directory.CreateDirectory(context.TempDirectory);
    }

    private async Task GenerateToTempFileAsync(
        ReportGenerationContext context, 
        string tempPath, 
        CancellationToken ct)
    {
        // Write to temporary file in the temp directory first
        var actualTempPath = Path.Combine(context.TempDirectory, Path.GetFileName(tempPath));
        
        await using (var fileStream = new FileStream(
            actualTempPath, 
            FileMode.Create, 
            FileAccess.Write, 
            FileShare.None, 
            bufferSize: 4096, 
            useAsync: true))
        {
            await GenerateContentAsync(context, fileStream, ct);
            await fileStream.FlushAsync(ct);
        }

        // Move from temp directory to target directory as .tmp
        if (File.Exists(tempPath))
            File.Delete(tempPath);
        
        File.Move(actualTempPath, tempPath);
    }

    private void PerformAtomicRename(string tempPath, string finalPath)
    {
        // Delete existing file if present (optional: could be configured)
        if (File.Exists(finalPath))
        {
            Logger.LogWarning(
                "[{ReportType}] Overwriting existing file: {FileName}",
                GetType().Name, Path.GetFileName(finalPath));
            File.Delete(finalPath);
        }

        // Atomic rename
        File.Move(tempPath, finalPath);
    }

    private void CleanupTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
                Logger.LogDebug("Cleaned up temporary file: {TempPath}", tempPath);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to cleanup temporary file: {TempPath}", tempPath);
        }
    }
}
