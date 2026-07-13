using MediatR;
using Microsoft.Extensions.Logging;
using PAFA.Domain.Constants;
using PAFA.Domain.Enums;
using PAFA.Domain.Interfaces;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Pipeline;
using PAFA.Infrastructure.Parsing;

namespace PAFA.Extraction.Handlers.Pipeline;

/// <summary>
/// Step 2 — Parse each imported file with ClosedXML and apply the six pipeline rules:
///
///   Rule 1 — Change of File Name    : structural base name differs from previous month
///                                     (date tokens are stripped before comparison;
///                                      SharePoint version counters "(N)" are also stripped)
///   Rule 2 — Change of Table Name   : sheet name is generic (Sheet1 / Feuil1 etc.)
///                                     unless the file type explicitly allows it
///   Rule 3 — Missing Field          : required column absent (per SourceFileRegistry)
///   Rule 4 — Change of Shippers     : shipper code found/missing vs known active list
///   Rule 5 — Invalid Value          : numeric value out of range (e.g. percentage > 100)
///   Rule 6 — Hidden Columns         : hidden columns detected in the workbook
///
/// Files that fail validation:
///   - Blob is moved from /inbound/ to /quarantine/
///   - A read-only URL for the quarantine folder is generated
///   - NO downstream processing (strict atomicity)
/// </summary>
public sealed class ParseAndValidateFilesHandler
    : IRequestHandler<ParseAndValidateFilesCommand, ParseAndValidateFilesResult>
{
    private static readonly string[] GenericSheetNames =
        ["Sheet1", "Sheet2", "Sheet3", "Feuil1", "Feuil2", "Feuil3"];

    private readonly IBlobStorageService _blobService;
    private readonly IUnitOfWork _uow;
    private readonly ExcelInspectionService _inspector;
    private readonly ILogger<ParseAndValidateFilesHandler> _log;

    public ParseAndValidateFilesHandler(
        IBlobStorageService blobService,
        IUnitOfWork uow,
        ExcelInspectionService inspector,
        ILogger<ParseAndValidateFilesHandler> log)
    {
        _blobService = blobService;
        _uow         = uow;
        _inspector   = inspector;
        _log         = log;
    }

    public async Task<ParseAndValidateFilesResult> Handle(
        ParseAndValidateFilesCommand cmd, CancellationToken ct)
    {
        var (importedFiles, year, month, correlationId) = cmd;

        _log.LogInformation(
            "Pipeline step {Step} {Status} — Files: {Count} — CorrelationId: {CorrelationId}",
            "ParseAndValidate", "Starting", importedFiles.Count, correlationId);

        // Pre-load shippers once for Rule 4
        // Index by BOTH Name and ShortCode so we can match whatever column the file uses.
        var knownShippers = await _uow.Shippers.GetActiveShippersAsync(ct);
        var knownShipperNames = knownShippers
            .SelectMany(s => new[]
            {
                s.Name?.Trim()      ?? string.Empty,
                s.ShortCode?.Trim() ?? string.Empty,
                s.AliasCode?.Trim() ?? string.Empty,
            })
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Pre-load previous month's file names for Rule 1
        var prevYear  = month == 1 ? year - 1 : year;
        var prevMonth = month == 1 ? 12        : month - 1;
        var prevFiles = await _uow.IngestionFiles.FindAsync(
            f => f.IngestionJob.ReportingPeriod.Year  == prevYear
              && f.IngestionJob.ReportingPeriod.Month == prevMonth, ct);

        var results = new List<ParseAndValidateResult>();

        foreach (var imported in importedFiles)
        {
            var errors = new List<PipelineValidationError>();

            // ── Parse ──────────────────────────────────────────────────────
            ExcelInspection inspection;
            try
            {
                using var stream = await _blobService.DownloadStreamAsync(imported.BlobPath, ct);
                inspection = _inspector.Inspect(stream, imported.FileName);
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "Failed to parse {FileName} — CorrelationId: {CorrelationId}",
                    imported.FileName, correlationId);

                errors.Add(new PipelineValidationError("Rule 0 — Parse Error",
                    [new ValidationExample(0, ex.Message)]));

                results.Add(await QuarantineAsync(
                    imported, year, month, correlationId, errors, ct));
                continue;
            }

            // ── Resolve file descriptor (SourceFileRegistry) ──────────────
            var descriptor = SourceFileRegistry.Match(imported.FileName);

            // ── Rule 1: Change of File Name ────────────────────────────────
            // Compare structural base names (date tokens and SharePoint version
            // counters stripped) so that a normal monthly date change in the
            // filename does NOT trigger a false positive.
            var currentBase = descriptor is not null
                ? SourceFileRegistry.GetBaseNameForComparison(imported.FileName, descriptor)
                : Path.GetFileNameWithoutExtension(imported.FileName);

            var prevSameTypeFiles = prevFiles
                .Where(f =>
                {
                    var d = SourceFileRegistry.Match(f.FileName);
                    // Match by file-key when descriptor is known; fall back to prefix.
                    if (descriptor is not null && d is not null)
                        return d.FileKey.Equals(descriptor.FileKey, StringComparison.OrdinalIgnoreCase);

                    // Legacy fallback: match by leading prefix token (e.g. "EUC09")
                    var legacyPrefix = f.FileName.Split("__", 2)[0].Split('_')[0];
                    return imported.FileName.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            if (prevSameTypeFiles.Count > 0)
            {
                var changed = prevSameTypeFiles
                    .Where(f =>
                    {
                        var prevBase = descriptor is not null
                            ? SourceFileRegistry.GetBaseNameForComparison(f.FileName, descriptor)
                            : Path.GetFileNameWithoutExtension(f.FileName);
                        return !prevBase.Equals(currentBase, StringComparison.OrdinalIgnoreCase);
                    })
                    .Take(10)
                    .Select((f, i) => new ValidationExample(i + 1,
                        $"Previous base: '{SourceFileRegistry.GetBaseNameForComparison(f.FileName, descriptor ?? SourceFileRegistry.Match(f.FileName)!)}' | Current base: '{currentBase}'"))
                    .ToList();

                if (changed.Count > 0)
                    errors.Add(new PipelineValidationError("Rule 1 — Change of File Name", changed));
            }

            // ── Rule 2: Change of Table Name (generic sheet names) ─────────
            // Skip Rule 2 for file types that are known to have generic sheet
            // names by design (e.g. DDP files delivered with Sheet1).
            bool allowGeneric = descriptor?.AllowGenericSheetNames ?? false;
            if (!allowGeneric)
            {
                var genericSheets = inspection.SheetNames
                    .Where(n => GenericSheetNames.Contains(n, StringComparer.OrdinalIgnoreCase)
                             || string.IsNullOrWhiteSpace(n))
                    .Take(10)
                    .Select((n, i) => new ValidationExample(i + 1,
                        $"Sheet with non-descriptive name: '{n}'"))
                    .ToList();

                if (genericSheets.Count > 0)
                    errors.Add(new PipelineValidationError("Rule 2 — Change of Table Name", genericSheets));
            }

            // ── Rule 3: Missing Field ──────────────────────────────────────
            // Required columns come from SourceFileRegistry when the file type
            // is known; the visible-column check is case-insensitive.
            var requiredCols = descriptor?.RequiredColumns ?? [];

            var missingCols = requiredCols
                .Where(col => !inspection.VisibleColumns
                    .Any(vc => vc.Equals(col, StringComparison.OrdinalIgnoreCase)))
                .Take(10)
                .Select((col, i) => new ValidationExample(i + 1,
                    $"Required column '{col}' not found in workbook"))
                .ToList();

            if (missingCols.Count > 0)
                errors.Add(new PipelineValidationError("Rule 3 — Missing Field", missingCols));

            // ── Rule 4: Change of Shippers ─────────────────────────────────
            // Find the first shipper-identifier column that exists in the workbook.
            // SourceFileRegistry provides the ordered list of aliases per file type.
            var shipperColumnAliases = descriptor?.ShipperColumnAliases
                ?? ["Shipper", "Shipper Short Code", "SRVC_PRVDR_CD"];

            var shipperColumn = shipperColumnAliases
                .FirstOrDefault(alias => inspection.VisibleColumns
                    .Any(vc => vc.Equals(alias, StringComparison.OrdinalIgnoreCase)));

            if (shipperColumn is not null)
            {
                var resolvedColumn = inspection.VisibleColumns
                    .First(vc => vc.Equals(shipperColumn, StringComparison.OrdinalIgnoreCase));

                var fileShippers = inspection.DataRows
                    .Select(r => r.Values.TryGetValue(resolvedColumn, out var v) ? v.Trim() : string.Empty)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var newShippers = fileShippers.Except(knownShipperNames).Take(10)
                    .Select((s, i) => new ValidationExample(i + 1,
                        $"New shipper: '{s}' (column: {resolvedColumn})"))
                    .ToList();

                var removedShippers = knownShipperNames.Except(fileShippers).Take(10)
                    .Select((s, i) => new ValidationExample(i + 1,
                        $"Missing shipper: '{s}' (column: {resolvedColumn})"))
                    .ToList();

                var shipperExamples = newShippers.Concat(removedShippers).Take(10).ToList();
                if (shipperExamples.Count > 0)
                    errors.Add(new PipelineValidationError("Rule 4 — Change of Shippers", shipperExamples));
            }

            // ── Rule 5: Invalid Value ──────────────────────────────────────
            var invalidValues = inspection.DataRows
                .SelectMany(row => row.Values
                    .Where(kv => kv.Key.Contains("rate", StringComparison.OrdinalIgnoreCase)
                              || kv.Key.Contains("percent", StringComparison.OrdinalIgnoreCase)
                              || kv.Key.Contains("%", StringComparison.OrdinalIgnoreCase))
                    .Where(kv => double.TryParse(kv.Value,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var d) && d > 100)
                    .Select(kv => new ValidationExample(row.RowNumber,
                        $"Column '{kv.Key}' = {kv.Value} (exceeds 100%)")))
                .Take(10)
                .ToList();

            if (invalidValues.Count > 0)
                errors.Add(new PipelineValidationError("Rule 5 — Invalid Value", invalidValues));

            // ── Rule 6: Hidden Columns ─────────────────────────────────────
            var hiddenExamples = inspection.HiddenColumns.Take(10)
                .Select((h, i) => new ValidationExample(i + 1,
                    $"Sheet '{h.SheetName}', column #{h.ColumnIndex}: '{h.ColumnName}'"))
                .ToList();

            if (hiddenExamples.Count > 0)
                errors.Add(new PipelineValidationError("Rule 6 — Hidden Columns", hiddenExamples));

            // ── Outcome ────────────────────────────────────────────────────
            if (errors.Count > 0)
            {
                var failedRules = string.Join(", ", errors.Select(e => e.RuleName));
                _log.LogWarning(
                    "Validation failed — CorrelationId: {CorrelationId} — File: {FileName} — Rules: {FailedRules} — Timestamp: {Timestamp}",
                    correlationId, imported.FileName, failedRules, DateTime.UtcNow);

                results.Add(await QuarantineAsync(
                    imported, year, month, correlationId, errors, ct));
            }
            else
            {
                _log.LogInformation(
                    "Validation passed — File: {FileName} — CorrelationId: {CorrelationId}",
                    imported.FileName, correlationId);

                results.Add(new ParseAndValidateResult(
                    imported.FileName,
                    imported.BlobPath,
                    ValidationStatus.Valid,
                    [],
                    null,
                    null));
            }
        }

        var validatedCount   = results.Count(r => r.Status == ValidationStatus.Valid);
        var quarantinedCount = results.Count - validatedCount;

        _log.LogInformation(
            "Pipeline step {Step} {Status} — Validated: {Valid}, Quarantined: {Quarantined} — CorrelationId: {CorrelationId}",
            "ParseAndValidate", "Completed", validatedCount, quarantinedCount, correlationId);

        return new ParseAndValidateFilesResult(true, results);
    }

    // ── Quarantine helper ─────────────────────────────────────────────────────

    private async Task<ParseAndValidateResult> QuarantineAsync(
        ImportedFile imported,
        int year, int month,
        Guid correlationId,
        IReadOnlyList<PipelineValidationError> errors,
        CancellationToken ct)
    {
        var quarantinePath = $"quarantine/{year:D4}/{month:D2}/{imported.FileName}";

        string? actualQuarantinePath = null;
        string? quarantineLink       = null;

        try
        {
            actualQuarantinePath = await _blobService.MoveAsync(
                imported.BlobPath, quarantinePath, ct);

            quarantineLink = await _blobService.GenerateReadUrlAsync(
                actualQuarantinePath,
                expiry: TimeSpan.FromDays(7),
                ct: ct);

            _log.LogInformation(
                "File quarantined — File: {FileName} — QuarantinePath: {Path} — CorrelationId: {CorrelationId}",
                imported.FileName, actualQuarantinePath, correlationId);
        }
        catch (Exception ex)
        {
            _log.LogError(ex,
                "Failed to move {FileName} to quarantine — CorrelationId: {CorrelationId}",
                imported.FileName, correlationId);
        }

        return new ParseAndValidateResult(
            imported.FileName,
            imported.BlobPath,
            ValidationStatus.Failed,
            errors,
            actualQuarantinePath,
            quarantineLink);
    }
}
