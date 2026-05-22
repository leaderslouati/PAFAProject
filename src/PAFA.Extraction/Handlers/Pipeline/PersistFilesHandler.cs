using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PAFA.Domain.Entities;
using PAFA.Domain.Enums;
using PAFA.Domain.Interfaces;
using PAFA.Domain.Models;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Pipeline;
using PAFA.Infrastructure.Services.PowerBi;
using PAFA.Notifications.Settings;

namespace PAFA.Extraction.Handlers.Pipeline;

/// <summary>
/// Step 3 — Persist pipeline results to the database and finalise blob locations.
///
/// Validated files:
///   - Blob moved: /inbound/ → /processed/
///   - SharePoint PATCH: ProcessingStatus = Processed
///   - IngestionFile record created (Status = Processed)
///
/// Quarantined files (already in /quarantine/ from Step 2):
///   - SharePoint PATCH: ProcessingStatus = Quarantined
///   - IngestionFile record created (Status = Failed) with ValidationError entities
///   - Service Bus notification published (even if overall pipeline is in Error state)
///
/// Post-processing:
///   - If at least one file was successfully persisted → Power BI dataset refresh triggered.
/// </summary>
public sealed class PersistFilesHandler : IRequestHandler<PersistFilesCommand, PersistFilesResult>
{
    private readonly IBlobStorageService _blobService;
    private readonly IRemoteFileSource _remoteSource;
    private readonly IUnitOfWork _uow;
    private readonly IEmailService _emailService;
    private readonly PowerBiDatasetRefreshService _pbiRefresh;
    private readonly NotificationSettings _notifSettings;
    private readonly PowerBiBatchExportSettings _pbiSettings;
    private readonly ILogger<PersistFilesHandler> _log;

    public PersistFilesHandler(
        IBlobStorageService blobService,
        IRemoteFileSource remoteSource,
        IUnitOfWork uow,
        IEmailService emailService,
        PowerBiDatasetRefreshService pbiRefresh,
        IOptions<NotificationSettings> notifSettings,
        PowerBiBatchExportSettings pbiSettings,
        ILogger<PersistFilesHandler> log)
    {
        _blobService   = blobService;
        _remoteSource  = remoteSource;
        _uow           = uow;
        _emailService  = emailService;
        _pbiRefresh    = pbiRefresh;
        _notifSettings = notifSettings.Value;
        _pbiSettings   = pbiSettings;
        _log           = log;
    }

    public async Task<PersistFilesResult> Handle(
        PersistFilesCommand cmd, CancellationToken ct)
    {
        var (validationResults, year, month, correlationId) = cmd;

        _log.LogInformation(
            "Pipeline step {Step} {Status} — Files: {Count} — CorrelationId: {CorrelationId}",
            "Persist", "Starting", validationResults.Count, correlationId);

        // ── Create one IngestionJob for this pipeline execution ────────────
        var job = new IngestionJob
        {
            JobName         = $"PIPELINE_{year}_{month:D2}",
            ReportingPeriod = new DateOnly(year, month, 1),
            Status          = IngestionJobStatus.Processing,
            FilesExpected   = validationResults.Count,
            StartedAt       = DateTime.UtcNow,
            TriggeredBy     = JobTrigger.Api,
            CorrelationId   = correlationId
        };
        await _uow.IngestionJobs.AddAsync(job, ct);
        await _uow.SaveChangesAsync(ct);

        var persistedResults    = new List<PersistedFileResult>();
        var validatedFileNames  = new List<string>();
        bool anyPersistFailure  = false;

        foreach (var result in validationResults)
        {
            if (result.Status == ValidationStatus.Valid)
            {
                bool failed = await PersistValidatedAsync(
                    result, job.Id, year, month, correlationId,
                    persistedResults, ct);
                if (failed) anyPersistFailure = true;
                validatedFileNames.Add(result.FileName);
            }
            else
            {
                await PersistQuarantinedAsync(
                    result, job.Id, year, month, correlationId,
                    persistedResults, ct);
            }
        }

        // ── Finalise IngestionJob ──────────────────────────────────────────
        job.FilesProcessed = persistedResults.Count(r => r.Status == "Persisted");
        job.FilesFailed    = persistedResults.Count(r => r.Status == "Quarantined");
        job.Status         = anyPersistFailure || job.FilesFailed > 0
            ? IngestionJobStatus.PartiallyCompleted
            : IngestionJobStatus.Completed;
        job.CompletedAt    = DateTime.UtcNow;
        _uow.IngestionJobs.Update(job);
        await _uow.SaveChangesAsync(ct);

        // ── Trigger Power BI refresh if any files were persisted ───────────
        bool pbiRefreshed = false;
        if (job.FilesProcessed > 0 && _pbiSettings.Datasets.Count > 0)
        {
            try
            {
                await _pbiRefresh.RefreshAllDatasetsAsync(_pbiSettings.Datasets, ct);
                pbiRefreshed = true;

                _log.LogInformation(
                    "Power BI dataset refresh triggered — CorrelationId: {CorrelationId}",
                    correlationId);
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "Power BI dataset refresh failed — CorrelationId: {CorrelationId}",
                    correlationId);
            }
        }

        _log.LogInformation(
            "Pipeline step {Step} {Status} — Persisted: {Persisted}, Quarantined: {Quarantined}, PBI: {Pbi} — CorrelationId: {CorrelationId}",
            "Persist", "Completed",
            job.FilesProcessed, job.FilesFailed, pbiRefreshed, correlationId);

        return new PersistFilesResult(
            !anyPersistFailure,
            new PersistenceReport(persistedResults, pbiRefreshed));
    }

    // ── Validated file ─────────────────────────────────────────────────────────

    private async Task<bool> PersistValidatedAsync(
        ParseAndValidateResult result,
        Guid jobId,
        int year, int month,
        Guid correlationId,
        List<PersistedFileResult> output,
        CancellationToken ct)
    {
        var processedPath = $"processed/{year:D4}/{month:D2}/{result.FileName}";

        try
        {
            var newBlobPath = await _blobService.MoveAsync(
                result.BlobPath, processedPath, ct);

            await _remoteSource.PatchStatusAsync(
                $"{year}/{month:D2}/{result.FileName}", "Processed", ct);

            var ingestionFile = new IngestionFile
            {
                IngestionJobId   = jobId,
                FileName         = result.FileName,
                BlobPath         = newBlobPath,
                Status           = IngestionFileStatus.Processed,
                ValidationStatus = ValidationStatus.Valid,
                ProcessedAt      = DateTime.UtcNow
            };
            await _uow.IngestionFiles.AddAsync(ingestionFile, ct);
            await _uow.SaveChangesAsync(ct);

            output.Add(new PersistedFileResult(
                result.FileName, "Persisted", ingestionFile.Id, newBlobPath));

            _log.LogInformation(
                "Pipeline step {Step} {Status} in {DurationMs}ms — JobId: {JobId} — File: {FileName} — CorrelationId: {CorrelationId}",
                "Persist", "FilePersisted", 0, jobId, result.FileName, correlationId);

            return false; // no failure
        }
        catch (Exception ex)
        {
            _log.LogError(ex,
                "Failed to persist validated file {FileName} \u2014 CorrelationId: {CorrelationId}",
                result.FileName, correlationId);

            output.Add(new PersistedFileResult(
                result.FileName, "Error", Guid.Empty, result.BlobPath));

            return true; // failure occurred
        }
    }

    // ── Quarantined file ───────────────────────────────────────────────────────

    private async Task PersistQuarantinedAsync(
        ParseAndValidateResult result,
        Guid jobId,
        int year, int month,
        Guid correlationId,
        List<PersistedFileResult> output,
        CancellationToken ct)
    {
        try
        {
            await _remoteSource.PatchStatusAsync(
                $"{year}/{month:D2}/{result.FileName}", "Quarantined", ct);

            // Persist IngestionFile record
            var ingestionFile = new IngestionFile
            {
                IngestionJobId   = jobId,
                FileName         = result.FileName,
                BlobPath         = result.QuarantineBlobPath ?? result.BlobPath,
                Status           = IngestionFileStatus.Failed,
                ValidationStatus = ValidationStatus.Failed,
                ErrorCount       = result.Errors.Count,
                ProcessedAt      = DateTime.UtcNow
            };
            await _uow.IngestionFiles.AddAsync(ingestionFile, ct);

            // Persist individual ValidationError entities (first 10 examples per rule)
            var validationErrors = result.Errors
                .SelectMany(e => e.Examples.Select(ex => new PAFA.Domain.Entities.ValidationError
                {
                    IngestionFileId = ingestionFile.Id,
                    LineNumber      = ex.RowNumber > 0 ? ex.RowNumber : null,
                    ErrorCode       = e.RuleName,
                    ErrorMessage    = ex.Value,
                    Severity        = "ERROR"
                }))
                .ToList();

            if (validationErrors.Count > 0)
                await _uow.IngestionFiles.AddValidationErrorsAsync(
                    ingestionFile.Id, validationErrors, ct);

            await _uow.SaveChangesAsync(ct);

            output.Add(new PersistedFileResult(
                result.FileName,
                "Quarantined",
                ingestionFile.Id,
                result.QuarantineBlobPath ?? result.BlobPath));

            // ── Publish Service Bus notification (always, regardless of overall status) ──
            await PublishQuarantineNotificationAsync(
                result, ingestionFile.Id, year, month, correlationId, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex,
                "Failed to persist quarantined file {FileName} — CorrelationId: {CorrelationId}",
                result.FileName, correlationId);

            output.Add(new PersistedFileResult(
                result.FileName, "Error", Guid.Empty, result.BlobPath));
        }
    }

    // ── Service Bus notification ───────────────────────────────────────────────

    private async Task PublishQuarantineNotificationAsync(
        ParseAndValidateResult result,
        Guid ingestionFileId,
        int year, int month,
        Guid correlationId,
        CancellationToken ct)
    {
        try
        {
            var errorItems = result.Errors
                .SelectMany(e => e.Examples.Take(10).Select(ex => new ValidationErrorItem(
                    RowNumber:     ex.RowNumber > 0 ? ex.RowNumber : null,
                    ColumnName:    null,
                    ErrorCode:     e.RuleName,
                    Severity:      "ERROR",
                    ErrorMessage:  $"{ex.Value} | QuarantineLink: {result.QuarantineFolderLink} | CorrelationId: {correlationId}",
                    OriginalValue: ex.Value)))
                .ToList();

            var context = new ValidationFailureEmailContext(
                IngestionFileId: ingestionFileId,
                FileName:        result.FileName,
                ReportingPeriod: $"{year}-{month:D2}",
                SourceSystem:    "PIPELINE",
                Recipients:      _notifSettings.ValidationFailureRecipients,
                AllErrors:       errorItems);

            await _emailService.SendValidationFailureAsync(context, ct);

            _log.LogInformation(
                "Service Bus validation-failure notification published — File: {FileName} — CorrelationId: {CorrelationId}",
                result.FileName, correlationId);
        }
        catch (Exception ex)
        {
            // Never let notification failure block the pipeline
            _log.LogError(ex,
                "Failed to publish Service Bus notification for {FileName} — CorrelationId: {CorrelationId}",
                result.FileName, correlationId);
        }
    }
}
