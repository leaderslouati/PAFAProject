using Microsoft.Extensions.Logging;
using PAFA.Reports.Batch.Configuration;
using PAFA.Reports.Batch.Models;

namespace PAFA.Reports.Batch.Core;

/// <summary>
/// Orchestrates the execution of multiple report generators.
/// Ensures that a failure in one generator does not stop the others.
/// 
/// ANTI-CRASH PATTERN:
/// - Each generator runs independently in a try-catch
/// - Failures are logged but don't crash the process
/// - Summary statistics are collected and reported
/// </summary>
public class BatchReportOrchestrator
{
    private readonly IEnumerable<ReportGenerator> _generators;
    private readonly BatchReportSettings _settings;
    private readonly ILogger<BatchReportOrchestrator> _logger;

    public BatchReportOrchestrator(
        IEnumerable<ReportGenerator> generators,
        BatchReportSettings settings,
        ILogger<BatchReportOrchestrator> logger)
    {
        _generators = generators;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Executes all configured report generators.
    /// Returns overall success status (true if at least one report succeeded).
    /// </summary>
    public async Task<bool> ExecuteAllAsync(CancellationToken ct = default)
    {
        var overallStartTime = DateTime.UtcNow;
        var results = new List<ReportGenerationResult>();

        _logger.LogInformation(
            "???????????????????????????????????????????????????????????");
        _logger.LogInformation(
            "PAFA Batch Report Generation Starting");
        _logger.LogInformation(
            "Target Period: {Year}-{Month:D2}", 
            _settings.TargetYear, _settings.TargetMonth);
        _logger.LogInformation(
            "Output Directory: {Directory}", _settings.OutputDirectory);
        _logger.LogInformation(
            "???????????????????????????????????????????????????????????");

        // Create base context
        var baseContext = new ReportGenerationContext
        {
            Year = _settings.TargetYear,
            Month = _settings.TargetMonth,
            OutputDirectory = _settings.OutputDirectory,
            TempDirectory = _settings.TempDirectory
        };

        // Determine which generators to run based on settings
        var generatorsToRun = _generators.Where(g => ShouldRunGenerator(g)).ToList();

        if (!generatorsToRun.Any())
        {
            _logger.LogWarning("No report generators are enabled in configuration.");
            return false;
        }

        _logger.LogInformation(
            "Running {Count} report generator(s): {Generators}",
            generatorsToRun.Count,
            string.Join(", ", generatorsToRun.Select(g => g.GetType().Name)));

        // Execute each generator
        foreach (var generator in generatorsToRun)
        {
            if (ct.IsCancellationRequested)
            {
                _logger.LogWarning("Cancellation requested. Stopping batch execution.");
                break;
            }

            // If specific shipper codes are configured, generate one report per shipper
            if (_settings.ShipperCodes.Any())
            {
                foreach (var shipperCode in _settings.ShipperCodes)
                {
                    var shipperContext = baseContext with { ShipperCode = shipperCode };
                    var result = await ExecuteGeneratorSafelyAsync(generator, shipperContext, ct);
                    results.Add(result);
                }
            }
            else
            {
                // Generate single aggregated report
                var result = await ExecuteGeneratorSafelyAsync(generator, baseContext, ct);
                results.Add(result);
            }
        }

        // Print summary
        PrintSummary(results, DateTime.UtcNow - overallStartTime);

        // Return true if at least one report succeeded
        return results.Any(r => r.Success);
    }

    private bool ShouldRunGenerator(ReportGenerator generator)
    {
        return generator switch
        {
            PdfReportGenerator => _settings.GeneratePdf,
            ExcelReportGenerator => _settings.GenerateExcel,
            _ => true // Unknown generators run by default
        };
    }

    private async Task<ReportGenerationResult> ExecuteGeneratorSafelyAsync(
        ReportGenerator generator, 
        ReportGenerationContext context, 
        CancellationToken ct)
    {
        try
        {
            return await generator.GenerateAsync(context, ct);
        }
        catch (Exception ex)
        {
            // This should never happen as ReportGenerator handles its own errors,
            // but we add an extra safety net here
            _logger.LogError(ex,
                "Unexpected error in {GeneratorType}. This should have been caught internally.",
                generator.GetType().Name);

            return ReportGenerationResult.Failed(
                generator.GetType().Name,
                $"Unexpected error: {ex.Message}",
                ex,
                TimeSpan.Zero);
        }
    }

    private void PrintSummary(List<ReportGenerationResult> results, TimeSpan totalDuration)
    {
        var successful = results.Count(r => r.Success);
        var failed = results.Count(r => !r.Success);
        var totalSize = results.Where(r => r.Success).Sum(r => r.FileSizeBytes);

        _logger.LogInformation(
            "???????????????????????????????????????????????????????????");
        _logger.LogInformation("BATCH EXECUTION SUMMARY");
        _logger.LogInformation("?????????????????????????????????????????????????????????");
        _logger.LogInformation("Total Reports: {Total}", results.Count);
        _logger.LogInformation("? Successful:  {Success}", successful);
        _logger.LogInformation("? Failed:      {Failed}", failed);
        _logger.LogInformation("Total Size:    {Size:N0} bytes", totalSize);
        _logger.LogInformation("Total Duration: {Duration:F2}s", totalDuration.TotalSeconds);
        _logger.LogInformation(
            "???????????????????????????????????????????????????????????");

        if (failed > 0)
        {
            _logger.LogWarning("Failed reports:");
            foreach (var result in results.Where(r => !r.Success))
            {
                _logger.LogWarning(
                    "  ? {ReportName}: {Error}",
                    result.ReportName, result.ErrorMessage);
            }
        }

        if (successful > 0)
        {
            _logger.LogInformation("Successful reports:");
            foreach (var result in results.Where(r => r.Success))
            {
                _logger.LogInformation(
                    "  ? {FileName} ({Size:N0} bytes, {Duration:F2}s)",
                    Path.GetFileName(result.FilePath!),
                    result.FileSizeBytes,
                    result.Duration.TotalSeconds);
            }
        }
    }
}
