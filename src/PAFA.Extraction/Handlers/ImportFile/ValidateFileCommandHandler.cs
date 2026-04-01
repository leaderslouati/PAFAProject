using MediatR;
using Microsoft.Extensions.Logging;
using PAFA.Domain.Entities;
using PAFA.Domain.Enums;
using PAFA.Domain.Interfaces;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Import;
using PAFA.Extraction.Services;
using PAFA.Infrastructure.Parsing;
using PAFA.Extraction.Validation;

namespace PAFA.Extraction.Handlers.ImportFile;

/// <summary>
/// Step 2 of the ingestion pipeline.
/// Applies all business rules (VAL-002..VAL-013) on the parsed rows
/// stored in the pipeline cache, persists ValidationError records,
/// and updates IngestionFile.ValidationStatus accordingly.
/// </summary>
public sealed class ValidateFileCommandHandler
    : IRequestHandler<ValidateFileCommand, ValidateFileResult>
{
    private readonly IUnitOfWork _uow;
    private readonly FilePipelineCache _cache;
    private readonly FileParserFactory _factory;
    private readonly IBlobStorageService _blob;
    private readonly ILogger<ValidateFileCommandHandler> _log;

    public ValidateFileCommandHandler(
        IUnitOfWork uow,
        FilePipelineCache cache,
        FileParserFactory factory,
        IBlobStorageService blob,
        ILogger<ValidateFileCommandHandler> log)
    {
        _uow = uow;
        _cache = cache;
        _factory = factory;
        _blob = blob;
        _log = log;
    }

    public async Task<ValidateFileResult> Handle(ValidateFileCommand cmd, CancellationToken ct)
    {
        // ?? 1. Load IngestionFile ?????????????????????????????????????
        var file = await _uow.IngestionFiles.GetByIdAsync(cmd.FileId, ct);
        if (file is null)
            return new ValidateFileResult(false, cmd.FileId, false, 0, 0, "Fichier introuvable en base de données.");

        // ?? 2. Retrieve parsed rows from cache. If missing, parse on-the-fly
        // to avoid relying on the pipeline cache lifetime.
        if (!_cache.TryGetParseResult(file.Id, out var rows, out var totalRows))
        {
            _log.LogInformation("[VALIDATE] Pas de cache — parsing à la volée pour {File}", file.FileName);

            // Try to resolve a parser and re-parse the file directly from blob storage
            FileParseResult parsed;
            try
            {
                var parser = _factory.GetParser(file.FileName);
                using var stream = await _blob.DownloadStreamAsync(file.BlobPath, ct);
                parsed = await parser.ParseAsync(stream, file.FileName, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[VALIDATE] Erreur parsing à la volée pour {File}", file.FileName);
                return new ValidateFileResult(false, file.Id, false, 0, 0,
                    $"Erreur lors du parsing : {ex.Message}");
            }

            if (!parsed.Success || parsed.Rows is null)
                return new ValidateFileResult(false, file.Id, false, 0, 0,
                    parsed.ErrorMessage ?? "Parsing échoué.");

            rows = parsed.Rows;
            totalRows = parsed.TotalRows;
        }

        // Ensure rows is not null before materializing
        rows ??= Array.Empty<RawDataRow>();

        _log.LogInformation("[VALIDATE] Démarrage — {File} | {Rows} lignes", file.FileName, totalRows);

        //  3. Apply business rules 
        var knownCodes = (await _uow.Shippers.GetActiveShippersAsync(ct))
            .Select(s => s.ShortCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var fakeParseResult = new FileParseResult
        {
            Success = true,
            ErrorMessage = null,
            FileName = file.FileName,
            DetectedFileType = string.Empty,
            Rows = rows.ToList(),
            RowsPerSheet = new Dictionary<string, int>()
        };
        var validator = new ImportValidationService(knownCodes);
        var validation = validator.Validate(fakeParseResult, file.FileName, isAnonymised: false);

        // ?? 4. Persist ValidationError records ????????????????????????
        if (validation.Findings.Count != 0)
        {
            var dbErrors = validation.Findings.Select(f => new ValidationError
            {
                IngestionFileId = file.Id,
                LineNumber      = f.RowNumber > 0 ? f.RowNumber : null,
                ColumnName      = f.FieldName,
                ErrorCode       = f.RuleId,
                ErrorMessage    = f.ErrorMessage,
                OriginalValue   = f.FieldValue,
                Severity        = f.Severity.ToString().ToUpperInvariant()
            }).ToList();

            await _uow.IngestionFiles.AddValidationErrorsAsync(file.Id, dbErrors, ct);
        }

        //  5. Update file validation status
        file.RowsValid    = validation.ValidRowCount;
        file.RowsRejected = validation.InvalidRowCount;
        file.ValidationStatus = validation.HasBlockingErrors
            ? ValidationStatus.Failed
            : validation.Findings.Any(f => f.Severity == ValidationSeverity.Warning)
                ? ValidationStatus.PassedWithWarnings
                : ValidationStatus.Passed;

        _uow.IngestionFiles.Update(file);
        await _uow.SaveChangesAsync(ct);

        _log.LogInformation(
            "[VALIDATE] OK — {File} | Blocking={Blocking} | Valid={Valid} | Rejected={Rejected}",
            file.FileName, validation.HasBlockingErrors,
            validation.ValidRowCount, validation.InvalidRowCount);

        return new ValidateFileResult(
            Success: true,
            FileId: file.Id,
            HasBlockingErrors: validation.HasBlockingErrors,
            ValidRowCount: validation.ValidRowCount,
            InvalidRowCount: validation.InvalidRowCount,
            ErrorMessage: null,
            Findings: validation.Findings);
    }
}
