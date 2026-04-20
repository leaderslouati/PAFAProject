// ═══════════════════════════════════════════════════════════
// PAFA.Infrastructure/Services/PowerBi/PowerBiBatchExportService.cs
// PURPOSE: Orchestrates the monthly batch export of 41 PARR
//          Power BI reports to Azure Blob Storage.
//
//  Flow:
//    1. Refresh Import-mode datasets (via PowerBiDatasetRefreshService)
//    2. Export each report as PDF (sequential, batched by 5)
//    3. Upload PDF to Blob Storage (IBlobStorageService)
//    4. Track each Report entity in PostgreSQL
//
//  Error isolation: if a single report fails, the error is
//  logged and the batch continues with the next report.
// ═══════════════════════════════════════════════════════════
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using PAFA.Domain.Entities;
using PAFA.Domain.Enums;
using PAFA.Domain.Interfaces;
using PAFA.Infrastructure.Persistence;

using DomainReport = PAFA.Domain.Entities.Report;

namespace PAFA.Infrastructure.Services.PowerBi;

public sealed class PowerBiBatchExportService(
    PowerBiClientFactory factory,
    PowerBiSettings powerBiSettings,
    PowerBiBatchExportSettings batchSettings,
    PowerBiDatasetRefreshService refreshService,
    IBlobStorageService blobStorage,
    PafaDbContext db,
    ILogger<PowerBiBatchExportService> logger) : IPowerBiBatchExportService
{
    // ─────────────────────────────────────────────────────────────────
    //  PUBLIC — Main Entry Point
    // ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<BatchExportResult> ExecuteMonthlyExportAsync(
        DateOnly reportingPeriod,
        CancellationToken ct = default)
    {
        var totalSw = Stopwatch.StartNew();
        var outcomes = new List<ReportExportOutcome>();

        logger.LogInformation(
            "═══ Batch Export — Period {Period:yyyy-MM}, {Count} reports configured ═══",
            reportingPeriod, batchSettings.Reports.Count);

        // ── Step 1: Refresh Import-mode datasets ────────────────────
        try
        {
            await refreshService.RefreshAllDatasetsAsync(batchSettings.Datasets, ct);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                "Dataset refresh failed — aborting entire batch export.");
            totalSw.Stop();
            return new BatchExportResult(
                reportingPeriod, batchSettings.Reports.Count,
                Succeeded: 0, Failed: batchSettings.Reports.Count,
                totalSw.Elapsed, outcomes);
        }

        // ── Step 2: Export reports in batches ────────────────────────
        var client = await factory.CreateAsync(ct);

        if (!Guid.TryParse(powerBiSettings.WorkspaceId, out var groupId))
        {
            var msg = $"WorkspaceId '{powerBiSettings.WorkspaceId}' is not a valid GUID. " +
                      "Set PowerBi:WorkspaceId to the shared workspace GUID (not 'me'). " +
                      "The workspace GUID appears in the Power BI URL: app.powerbi.com/groups/{{GUID}}/...";
            logger.LogCritical(msg);
            totalSw.Stop();
            return new BatchExportResult(
                reportingPeriod, batchSettings.Reports.Count,
                Succeeded: 0, Failed: batchSettings.Reports.Count,
                totalSw.Elapsed, outcomes);
        }

        // Pre-load ReportType lookup (SCH2A → id, SCH2B → id)
        var reportTypes = await db.Set<ReportType>()
            .Where(rt => !rt.IsDeleted)
            .ToDictionaryAsync(rt => rt.Code, rt => rt, ct);

        var batches = batchSettings.Reports
            .Chunk(batchSettings.BatchSize)
            .ToList();

        for (var batchIdx = 0; batchIdx < batches.Count; batchIdx++)
        {
            var batch = batches[batchIdx];

            logger.LogInformation(
                "Processing batch {Batch}/{Total} ({Count} reports)",
                batchIdx + 1, batches.Count, batch.Length);

            foreach (var reportDef in batch)
            {
                var outcome = await ExportAndTrackSingleReportAsync(
                    client, groupId, reportDef, reportingPeriod, reportTypes, ct);
                outcomes.Add(outcome);
            }

            // Throttle between batches — Power BI API limits:
            //   5 concurrent exports per tenant, ~50 exports/hour
            if (batchIdx < batches.Count - 1)
            {
                logger.LogDebug(
                    "Throttle pause: {Delay}s between batches",
                    batchSettings.ThrottleDelaySeconds);
                await Task.Delay(
                    TimeSpan.FromSeconds(batchSettings.ThrottleDelaySeconds), ct);
            }
        }

        // ── Step 3: Summary ─────────────────────────────────────────
        totalSw.Stop();
        var succeeded = outcomes.Count(o => o.Success);
        var failed = outcomes.Count(o => !o.Success);

        logger.LogInformation(
            "═══ Batch Export Complete — {Succeeded}/{Total} ok, {Failed} failed, {Duration:F0}s ═══",
            succeeded, outcomes.Count, failed, totalSw.Elapsed.TotalSeconds);

        return new BatchExportResult(
            reportingPeriod, outcomes.Count, succeeded, failed,
            totalSw.Elapsed, outcomes);
    }

    // ─────────────────────────────────────────────────────────────────
    //  PRIVATE — Export + Track (error-isolated per report)
    // ─────────────────────────────────────────────────────────────────

    private async Task<ReportExportOutcome> ExportAndTrackSingleReportAsync(
        PowerBIClient client,
        Guid groupId,
        ReportDefinition reportDef,
        DateOnly reportingPeriod,
        Dictionary<string, ReportType> reportTypes,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            ct.ThrowIfCancellationRequested();

            // ── Export PDF from Power BI ──────────────────────────────
            using var pdfStream = await ExportSingleReportAsFileAsync(
                client, groupId, reportDef.PowerBiReportId, FileFormat.PDF, ct);

            var pdfFileName = BuildFileName(reportDef, reportingPeriod, ".pdf");
            var pdfBlobPath = await blobStorage.UploadAsync(
                pdfFileName, pdfStream, batchSettings.BlobContainer, ct: ct);

            logger.LogInformation(
                "✓ {ScheduleRef} PDF uploaded → {BlobPath}",
                reportDef.ScheduleRef, pdfBlobPath);

            // ── Export PPTX from Power BI ─────────────────────────────
            string? pptxBlobPath = null;
            try
            {
                using var pptxStream = await ExportSingleReportAsFileAsync(
                    client, groupId, reportDef.PowerBiReportId, FileFormat.PPTX, ct);

                var pptxFileName = BuildFileName(reportDef, reportingPeriod, ".pptx");
                pptxBlobPath = await blobStorage.UploadAsync(
                    pptxFileName, pptxStream, batchSettings.BlobContainer, ct: ct);

                logger.LogInformation(
                    "✓ {ScheduleRef} PPTX uploaded → {BlobPath}",
                    reportDef.ScheduleRef, pptxBlobPath);
            }
            catch (Exception ex)
            {
                // PPTX failure is non-blocking — PDF is the primary format
                logger.LogWarning(ex,
                    "⚠ {ScheduleRef} PPTX export failed (PDF succeeded): {Error}",
                    reportDef.ScheduleRef, ex.Message);
            }

            // ── Persist Report entity in DB ──────────────────────────
            await TrackReportAsync(
                reportDef, reportingPeriod, pdfBlobPath, pptxBlobPath, reportTypes, ct);

            sw.Stop();
            logger.LogInformation(
                "✓ {ScheduleRef} exported in {Duration:F1}s → PDF:{PdfPath} PPTX:{PptxPath}",
                reportDef.ScheduleRef, sw.Elapsed.TotalSeconds, pdfBlobPath, pptxBlobPath ?? "N/A");

            return new ReportExportOutcome(
                reportDef.ScheduleRef, reportDef.Title,
                Success: true, pdfBlobPath, ErrorMessage: null, sw.Elapsed);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex,
                "✗ {ScheduleRef} failed after {Duration:F1}s: {Error}",
                reportDef.ScheduleRef, sw.Elapsed.TotalSeconds, ex.Message);

            return new ReportExportOutcome(
                reportDef.ScheduleRef, reportDef.Title,
                Success: false, BlobPath: null, ex.Message, sw.Elapsed);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  PRIVATE — Power BI PDF Export with Polling
    // ─────────────────────────────────────────────────────────────────

    private async Task<Stream> ExportSingleReportAsFileAsync(
        PowerBIClient client,
        Guid groupId,
        string reportId,
        FileFormat format,
        CancellationToken ct)
    {
        var rptGuid = Guid.Parse(reportId);

        // No EffectiveIdentity → admin-level export (PafaAdmin sees all data).
        // For Industry (2A) views: anonymisation is enforced by the SQL view itself.
        var exportRequest = new ExportReportRequest { Format = format };

        var exportJob = await client.Reports
            .ExportToFileInGroupAsync(groupId, rptGuid, exportRequest);

        logger.LogDebug(
            "Export kicked off: ExportId={ExportId} ReportId={ReportId}",
            exportJob.Id, reportId);

        // ── Poll until Succeeded or Failed ──────────────────────────
        var deadline = DateTime.UtcNow.AddSeconds(batchSettings.ExportTimeoutSeconds);
        var pollMs = batchSettings.ExportPollIntervalSeconds * 1_000;

        Export? status = null;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(pollMs, ct);

            status = await client.Reports
                .GetExportToFileStatusInGroupAsync(groupId, rptGuid, exportJob.Id);

            logger.LogDebug(
                "Export poll — ExportId={ExportId} Status={Status} Pct={Pct}%",
                exportJob.Id, status.Status, status.PercentComplete);

            if (status.Status is ExportState.Succeeded or ExportState.Failed)
                break;
        }

        if (status?.Status != ExportState.Succeeded)
        {
            throw new InvalidOperationException(
                $"Power BI export did not complete. " +
                $"Status: {status?.Status?.ToString() ?? "Timeout"}. " +
                $"ExportId: {exportJob.Id}");
        }

        // ── Download the rendered PDF ───────────────────────────────
        return await client.Reports
            .GetFileOfExportToFileInGroupAsync(groupId, rptGuid, exportJob.Id);
    }

    // ─────────────────────────────────────────────────────────────────
    //  PRIVATE — DB Tracking
    // ─────────────────────────────────────────────────────────────────

    private async Task TrackReportAsync(
        ReportDefinition reportDef,
        DateOnly reportingPeriod,
        string pdfBlobPath,
        string? pptxBlobPath,
        Dictionary<string, ReportType> reportTypes,
        CancellationToken ct)
    {
        if (!reportTypes.TryGetValue(reportDef.ReportTypeCode, out var reportType))
        {
            logger.LogWarning(
                "ReportTypeCode '{Code}' not found in DB — skipping tracking for {Ref}",
                reportDef.ReportTypeCode, reportDef.ScheduleRef);
            return;
        }

        var audience = reportDef.Audience.Equals("Industry", StringComparison.OrdinalIgnoreCase)
            ? ReportAudience.Industry
            : ReportAudience.PAC;

        // Upsert: update if existing, insert if new
        var existing = await db.Set<DomainReport>()
            .FirstOrDefaultAsync(r =>
                r.ReportTypeId == reportType.Id &&
                r.ScheduleNumber == reportDef.ScheduleNumber &&
                r.ReportingPeriod == reportingPeriod &&
                !r.IsDeleted, ct);

        if (existing is not null)
        {
            existing.Status = ReportStatus.Generated;
            existing.GeneratedAt = DateTime.UtcNow;
            existing.FilePath_PDF = pdfBlobPath;
            if (pptxBlobPath is not null)
                existing.FilePath_PPTX = pptxBlobPath;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = "BatchExportService";
        }
        else
        {
            db.Set<DomainReport>().Add(new DomainReport
            {
                ReportTypeId = reportType.Id,
                ScheduleNumber = reportDef.ScheduleNumber,
                Title = reportDef.Title,
                ReportingPeriod = reportingPeriod,
                Audience = audience,
                Status = ReportStatus.Generated,
                GeneratedAt = DateTime.UtcNow,
                FilePath_PDF = pdfBlobPath,
                FilePath_PPTX = pptxBlobPath,
                CreatedBy = "BatchExportService"
            });
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Builds the blob filename: PAFA_2A_1_2026_03.pdf
    /// </summary>
    private static string BuildFileName(ReportDefinition def, DateOnly period, string extension = ".pdf")
        => $"PAFA_{def.ScheduleRef.Replace(".", "_")}_{period:yyyy_MM}{extension}";
}