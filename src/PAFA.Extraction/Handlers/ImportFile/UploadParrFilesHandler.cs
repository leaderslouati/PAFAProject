using MediatR;
using Microsoft.Extensions.Logging;
using PAFA.Domain.Entities;
using PAFA.Domain.Enums;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Mapping;
using PAFA.Extraction.Validation;
using PAFA.Extraction.Validations;
using PAFA.Infrastructure.Parsing;

namespace PAFA.Extraction.Commands.Import;

public class UploadParrFilesCommandHandler
    : IRequestHandler<UploadParrFilesCommand, UploadParrFilesResult>
{
    // ── Well-known prefixes used when no IFileSourceSettings is available ──
    private static readonly IReadOnlyList<string> DefaultAllowedPrefixes =
        ["MOD520A", "RPT_1364", "MOD700", "EUC09", "TRANSFER", "CLASS4AQ"];

    private static readonly IReadOnlyList<string> DefaultAllowedExtensions =
        [".xlsx", ".xls", ".csv", ".xml"];

    private readonly IUnitOfWork _uow;
    private readonly FileParserFactory _factory;
    private readonly ILogger<UploadParrFilesCommandHandler> _log;

    public UploadParrFilesCommandHandler(
        IUnitOfWork uow, FileParserFactory factory,
        ILogger<UploadParrFilesCommandHandler> log)
    { _uow = uow; _factory = factory; _log = log; }

    public async Task<UploadParrFilesResult> Handle(
        UploadParrFilesCommand cmd, CancellationToken ct)
    {
        _log.LogInformation("Import démarré — {File}", cmd.FileName);
        var period = new DateOnly(cmd.PeriodYear, cmd.PeriodMonth, 1);

        // ── VAL-001 / NAME-001..004 : file name convention pre-check ──────
        // Applied before creating any DB record so no partial ingestion_file
        // row is left on a naming violation.
        var nameValidation = FileNameValidator.Validate(
            cmd.FileName, DefaultAllowedPrefixes, DefaultAllowedExtensions);

        if (!nameValidation.IsValid)
        {
            var summary = string.Join("; ",
                nameValidation.Findings
                    .Where(f => f.Severity == "ERROR")
                    .Select(f => $"[{f.RuleId}] {f.Message}"));

            _log.LogWarning("File name rejected — {File} | {Summary}", cmd.FileName, summary);

            // Return failure without touching the database
            return new UploadParrFilesResult(
                Success: false,
                JobId: Guid.Empty,
                FileId: Guid.Empty,
                FileName: cmd.FileName,
                RowsRead: 0, RowsValid: 0, RowsRejected: 0,
                ErrorMessage: $"File name validation failed: {summary}");
        }

        // Log warnings (e.g. month token unreadable) but continue
        foreach (var w in nameValidation.Findings.Where(f => f.Severity == "WARNING"))
            _log.LogWarning("[{RuleId}] {File} — {Msg}", w.RuleId, cmd.FileName, w.Message);

        // ── 1. Créer IngestionJob + IngestionFile ─────────────────
        var job = new IngestionJob
        {
            JobName         = $"PARR_{cmd.PeriodYear}_{cmd.PeriodMonth:D2}",
            ReportingPeriod = period,
            Status          = IngestionJobStatus.Processing,
            FilesExpected   = 1,
            StartedAt       = DateTime.UtcNow,
            TriggeredBy     = cmd.JobTrigger,
            ParentJobId     = cmd.ParentJobId,
            RetryCount      = cmd.RetryCount
        };
        await _uow.IngestionJobs.AddAsync(job, ct);

        var file = new IngestionFile
        {
            IngestionJobId = job.Id,
            FileName = cmd.FileName,
            SourceSystem = cmd.SourceSystem,
            FileSizeBytes = cmd.FileContent.Length,
            BlobPath = cmd.BlobPath,
            Status = IngestionFileStatus.Validating,
            ValidationStatus = ValidationStatus.Pending,
            DownloadedAt = DateTime.UtcNow
        };
        await _uow.IngestionFiles.AddAsync(file, ct);
        await _uow.SaveChangesAsync(ct);

        try
        {
            // ── 2. Parser ─────────────────────────────────────────
            IFileParser parser;
            try
            {
                parser = _factory.GetParser(cmd.FileName);
            }
            catch (NotSupportedException nse)
            {
                _log.LogWarning(nse, "Unsupported file format: {File}", cmd.FileName);
                return await Fail(job, file, nse.Message, 0, 0, 0, ct);
            }

            using var ms = new MemoryStream(cmd.FileContent);
            var parsed = await parser.ParseAsync(ms, cmd.FileName, ct);

            if (!parsed.Success)
                return await Fail(job, file, parsed.ErrorMessage ?? "Erreur parsing", 0, 0, 0, ct);

            // ── 3. Valider ────────────────────────────────────────
            var knownCodes = (await _uow.Shippers.GetActiveShippersAsync(ct))
                .Select(s => s.ShortCode).ToHashSet();
            var validator = new ImportValidationService(knownCodes);
            var validation = validator.Validate(parsed, cmd.FileName, isAnonymised: false);

            // ── 4. Persister les erreurs ──────────────────────────
            if (validation.Findings.Any())
            {
                var dbErrors = validation.Findings.Select(f => new ValidationError
                {
                    IngestionFileId = file.Id,
                    LineNumber = f.RowNumber > 0 ? f.RowNumber : null,
                    ColumnName = f.FieldName,
                    ErrorCode = f.RuleId,
                    ErrorMessage = f.ErrorMessage,
                    OriginalValue = f.FieldValue,
                    Severity = f.Severity.ToString().ToUpperInvariant()
                }).ToList();
                await _uow.IngestionFiles.AddValidationErrorsAsync(file.Id, dbErrors, ct);
            }

            // ── 5. Erreurs bloquantes → réponse echec ─────────────
            if (validation.HasBlockingErrors)
            {
                var summary = string.Join("; ",
                    validation.Findings
                        .Where(f => f.Severity == ValidationSeverity.Error)
                        .Take(5).Select(f => f.ErrorMessage));
                return await Fail(job, file, summary,
                    parsed.TotalRows, validation.ValidRowCount, validation.InvalidRowCount, ct);
            }

            // ── 6. Mapper → MetricValues ──────────────────────────
            var badRows = validation.Findings
                .Where(f => f.Severity == ValidationSeverity.Error && f.RowNumber > 0)
                .Select(f => f.RowNumber).ToHashSet();

            var metrics = new List<MetricValue>();
            foreach (var row in parsed.Rows)
            {
                if (badRows.Contains(row.RowNumber)) continue;
                metrics.AddRange(MetricValueMapper.MapToMetricValues(
                    row, file.Id, period, cmd.UploadedBy));
            }

            if (metrics.Any())
                await _uow.MetricValues.AddRangeAsync(metrics, ct);

            // ── 7. Mettre à jour les statuts ──────────────────────
            file.Status = IngestionFileStatus.Loaded;
            file.ValidationStatus = validation.Findings.Any(f => f.Severity == ValidationSeverity.Warning)
                ? ValidationStatus.PassedWithWarnings : ValidationStatus.Passed;
            file.RowsRead = parsed.TotalRows;
            file.RowsValid = validation.ValidRowCount;
            file.RowsRejected = validation.InvalidRowCount;
            file.ProcessedAt = DateTime.UtcNow;
            _uow.IngestionFiles.Update(file);

            job.Status = IngestionJobStatus.Completed;
            job.FilesProcessed = 1;
            job.RecordsLoaded = metrics.Count;
            job.CompletedAt = DateTime.UtcNow;
            _uow.IngestionJobs.Update(job);

            await _uow.SaveChangesAsync(ct);

            _log.LogInformation("Import OK — {Rows} lignes Excel, {Metrics} métriques insérées",
                validation.ValidRowCount, metrics.Count);

            return new UploadParrFilesResult(
                Success: true, JobId: job.Id, FileId: file.Id,
                FileName: cmd.FileName, RowsRead: parsed.TotalRows,
                RowsValid: validation.ValidRowCount,
                RowsRejected: validation.InvalidRowCount,
                ErrorMessage: null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Erreur inattendue — {File}", cmd.FileName);
            return await Fail(job, file, ex.Message, 0, 0, 0, ct);
        }
    }

    async Task<UploadParrFilesResult> Fail(
        IngestionJob job, IngestionFile file, string err,
        int total, int valid, int rejected, CancellationToken ct)
    {
        file.Status = IngestionFileStatus.Failed;
        file.ValidationStatus = ValidationStatus.Failed;
        file.RowsRead = total; file.RowsValid = valid; file.RowsRejected = rejected;
        _uow.IngestionFiles.Update(file);
        job.Status = IngestionJobStatus.Failed;
        job.ErrorSummary = err; job.CompletedAt = DateTime.UtcNow;
        _uow.IngestionJobs.Update(job);
        await _uow.SaveChangesAsync(ct);
        return new UploadParrFilesResult(false, job.Id, file.Id,
            file.FileName, total, valid, rejected, err);
    }
}