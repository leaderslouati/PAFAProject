using MediatR;
using Microsoft.Extensions.Logging;
using PAFA.Domain.Entities;
using PAFA.Domain.Enums;
using PAFA.Domain.Interfaces;
using System.Linq;
using System.Collections.Generic;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Import;
using PAFA.Extraction.Mapping;
using PAFA.Extraction.Services;
using PAFA.Extraction.Validation;
using PAFA.Infrastructure.Parsing;

namespace PAFA.Extraction.Handlers.ImportFile;

/// <summary>
/// Step 3 (final) of the ingestion pipeline.
/// Persists MetricValues for all valid rows, then moves the blob:
///   ? "processed/{year}/{month}/{file}"  when validation passed
///   ? "failed/{year}/{month}/{file}"     when blocking errors exist
/// Updates final statuses on IngestionFile and IngestionJob.
/// Clears the pipeline cache entry.
/// </summary>
public sealed class PersistFileCommandHandler
    : IRequestHandler<PersistFileCommand, PersistFileResult>
{
    private readonly IUnitOfWork _uow;
    private readonly IBlobStorageService _blob;
    private readonly FilePipelineCache _cache;
    private readonly FileParserFactory _factory;
    private readonly ILogger<PersistFileCommandHandler> _log;

    public PersistFileCommandHandler(
        IUnitOfWork uow,
        IBlobStorageService blob,
        FilePipelineCache cache,
        FileParserFactory factory,
        ILogger<PersistFileCommandHandler> log)
    {
        _uow = uow;
        _blob = blob;
        _cache = cache;
        _factory = factory;
        _log = log;
    }

    public async Task<PersistFileResult> Handle(PersistFileCommand cmd, CancellationToken ct)
    {
        // ?? 1. Load IngestionFile + IngestionJob ??????????????????????
        var file = await _uow.IngestionFiles.GetByIdAsync(cmd.FileId, ct);
        if (file is null)
            return new PersistFileResult(false, cmd.FileId, 0, null, "Fichier introuvable en base de données.");

        var job = await _uow.IngestionJobs.GetByIdAsync(file.IngestionJobId, ct);
        if (job is null)
            return new PersistFileResult(false, file.Id, 0, null, "Job introuvable en base de données.");

        // ?? 2. Guard: validation must have been run ???????????????????
        if (file.ValidationStatus == ValidationStatus.Pending)
            return new PersistFileResult(false, file.Id, 0, null,
                "Le fichier doit d'abord être validé. Appelez /validate avant /persist.");

        // ?? 3. If blocking errors ? move to failed, no DB insert ?????
        if (file.ValidationStatus == ValidationStatus.Failed)
        {
            var failedBlobPath = await MoveBlobSafe(file.BlobPath, "failed", ct);
            return await FinalizeFailed(
                job, file,
                "Validation échouée — erreurs bloquantes. Fichier déplacé vers failed.",
                failedBlobPath, ct);
        }

        // ?? 4. Retrieve parsed rows from cache — if absent, re-parse on-the-fly
        if (!_cache.TryGetParseResult(file.Id, out var rows, out _))
        {
            _log.LogInformation("[PERSIST] Pas de cache — parsing à la volée pour {File}", file.FileName);

            try
            {
                var parser = _factory.GetParser(file.FileName);
                using var stream = await _blob.DownloadStreamAsync(file.BlobPath, ct);
                var parsed = await parser.ParseAsync(stream, file.FileName, ct);
                if (!parsed.Success || parsed.Rows is null)
                    return new PersistFileResult(false, file.Id, 0, null,
                        parsed.ErrorMessage ?? "Parsing échoué lors de la persistance.");

                rows = parsed.Rows;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[PERSIST] Erreur parsing à la volée pour {File}", file.FileName);
                return new PersistFileResult(false, file.Id, 0, null, ex.Message);
            }
        }

        rows ??= Array.Empty<RawDataRow>();

        _log.LogInformation("[PERSIST] Démarrage — {File} | {Rows} lignes à persister", file.FileName, rows.Count);

        try
        {
            // ?? 5. Filter out error rows ??????????????????????????????
            var errorRows = (await _uow.IngestionFiles.GetValidationErrorsAsync(file.Id, ct))
                .Where(e => e.Severity == "ERROR" && e.LineNumber.HasValue)
                .Select(e => e.LineNumber!.Value)
                .ToHashSet();

            var metrics = new List<MetricValue>();
            foreach (var row in rows)
            {
                if (errorRows.Contains(row.RowNumber)) continue;
                metrics.AddRange(MetricValueMapper.MapToMetricValues(row, file.Id, job.ReportingPeriod));
            }

            if (metrics.Count != 0)
                await _uow.MetricValues.AddRangeAsync(metrics, ct);

            // ?? 6. Move blob ? processed 
            var processedBlobPath = await MoveBlobSafe(file.BlobPath, "processed", ct);

            // ?? 7. Finalize statuses 
            file.BlobPath     = processedBlobPath;
            file.Status       = IngestionFileStatus.Processed;
            file.ProcessedAt  = DateTime.UtcNow;
            _uow.IngestionFiles.Update(file);

            job.Status         = IngestionJobStatus.Completed;
            job.FilesProcessed = 1;
            job.RecordsLoaded  = metrics.Count;
            job.CompletedAt    = DateTime.UtcNow;
            _uow.IngestionJobs.Update(job);

            await _uow.SaveChangesAsync(ct);

            // ?? 8. Clear cache entry 
            _cache.Remove(file.Id);

            _log.LogInformation(
                "[PERSIST] OK — {File} | {Metrics} métriques | BlobPath: {Path}",
                file.FileName, metrics.Count, processedBlobPath);

            return new PersistFileResult(true, file.Id, metrics.Count, processedBlobPath, null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[PERSIST] Erreur inattendue — {File}", file.FileName);
            var failedBlobPath = await MoveBlobSafe(file.BlobPath, "failed", ct);
            return await FinalizeFailed(job, file, ex.Message, failedBlobPath, ct);
        }
    }

    // ?? Helpers ????????????????????????????????????????????????????????

    /// <summary>Moves blob to target container; logs warning on failure (non-blocking).</summary>
    private async Task<string> MoveBlobSafe(string? sourcePath, string targetContainer, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sourcePath)
            || sourcePath.StartsWith($"{targetContainer}/", StringComparison.OrdinalIgnoreCase))
            return sourcePath ?? string.Empty;

        var destPath = BuildTargetPath(sourcePath, targetContainer);
        try
        {
            return await _blob.MoveAsync(sourcePath, destPath, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[PERSIST] Impossible de déplacer le blob vers {Target} — {Path}",
                targetContainer, destPath);
            return sourcePath;
        }
    }

    private async Task<PersistFileResult> FinalizeFailed(
        Domain.Entities.IngestionJob job,
        Domain.Entities.IngestionFile file,
        string err,
        string finalBlobPath,
        CancellationToken ct)
    {
        file.BlobPath         = finalBlobPath;
        file.Status           = IngestionFileStatus.Failed;
        file.ProcessedAt      = DateTime.UtcNow;
        _uow.IngestionFiles.Update(file);

        job.Status       = IngestionJobStatus.Failed;
        job.ErrorSummary = err;
        job.CompletedAt  = DateTime.UtcNow;
        _uow.IngestionJobs.Update(job);

        await _uow.SaveChangesAsync(ct);
        _cache.Remove(file.Id);

        return new PersistFileResult(false, file.Id, 0, finalBlobPath, err);
    }

    private static string BuildTargetPath(string blobPath, string targetContainer)
    {
        if (string.IsNullOrWhiteSpace(blobPath))
            return $"{targetContainer}/";

        var parts = blobPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return $"{targetContainer}/{parts[0]}";

        // Known semantic prefixes that may appear as first or second segment
        var knownPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "inbound",
            "processed",
            "failed",
        };

        // Case: "data/inbound/2025/06/file" => drop the 'inbound' segment and produce "processed/2025/06/file"
        if (parts.Length >= 2 && knownPrefixes.Contains(parts[1]))
        {
            var rest = string.Join('/', parts.Skip(2));
            return $"{targetContainer}/{rest}";
        }

        // Case: "inbound/2025/06/file" or "landing-zone/2025/06/file" => drop first segment
        if (knownPrefixes.Contains(parts[0]))
        {
            var rest = string.Join('/', parts.Skip(1));
            return $"{targetContainer}/{rest}";
        }

        // Fallback: assume first segment is base bucket and keep remainder
        var fallback = string.Join('/', parts.Skip(1));
        return $"{targetContainer}/{fallback}";
    }
}
