using MediatR;
using Microsoft.Extensions.Logging;
using PAFA.Domain.Entities;
using PAFA.Domain.Enums;
using PAFA.Domain.Repositories;
using PAFA.Extraction.Commands.Import;
using PAFA.Extraction.Validations;

namespace PAFA.Extraction.Handlers.ImportFile;

/// <summary>
/// Handler for UploadParrFilesCommand — used by the SharePoint automated pipeline
/// (DownloadParrFilesCommandHandler) and kept for backward compatibility.
///
/// Internally orchestrates the three SRP steps:
///   InitiateFileUpload (job+file creation) ? Parse ? Validate ? Persist
/// so that the SharePoint flow remains a single MediatR call.
/// </summary>
public sealed class UploadParrFilesCommandHandler
    : IRequestHandler<UploadParrFilesCommand, UploadParrFilesResult>
{
    private static readonly string[] AllowedPrefixes =
        ["MOD520A", "RPT_1364", "MOD700", "EUC09", "TRANSFER", "CLASS4AQ"];
    private static readonly string[] AllowedExtensions = [".xlsx", ".xls"];

    private readonly IUnitOfWork _uow;
    private readonly IMediator _mediator;
    private readonly ILogger<UploadParrFilesCommandHandler> _log;

    public UploadParrFilesCommandHandler(
        IUnitOfWork uow,
        IMediator mediator,
        ILogger<UploadParrFilesCommandHandler> log)
    {
        _uow = uow;
        _mediator = mediator;
        _log = log;
    }

    public async Task<UploadParrFilesResult> Handle(
        UploadParrFilesCommand cmd, CancellationToken ct)
    {
        _log.LogInformation("[UPLOAD_PARR] Démarrage — {File}", cmd.FileName);

        // ?? 0. File name validation ???????????????????????????????????
        var nameValidation = FileNameValidator.Validate(cmd.FileName, AllowedPrefixes, AllowedExtensions);
        if (!nameValidation.IsValid)
        {
            var summary = string.Join("; ", nameValidation.Findings
                .Where(f => f.Severity == "ERROR")
                .Select(f => $"[{f.RuleId}] {f.Message}"));
            _log.LogWarning("[UPLOAD_PARR] Nom rejeté — {File} | {Summary}", cmd.FileName, summary);
            return Fail(Guid.Empty, Guid.Empty, cmd.FileName, $"File name validation failed: {summary}");
        }

        // ?? 1. Create IngestionJob + IngestionFile ????????????????????
        var period = new DateOnly(cmd.PeriodYear, cmd.PeriodMonth, 1);
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
            IngestionJobId   = job.Id,
            FileName         = cmd.FileName,
            BlobPath         = cmd.BlobPath,
            Status           = IngestionFileStatus.Validating,
            ValidationStatus = ValidationStatus.Pending,
            DownloadedAt     = DateTime.UtcNow
        };
        await _uow.IngestionFiles.AddAsync(file, ct);
        await _uow.SaveChangesAsync(ct);

        // ?? 2. Parse ??????????????????????????????????????????????????
        var parseResult = await _mediator.Send(new ParseFileCommand(file.Id), ct);
        if (!parseResult.Success)
            return Fail(job.Id, file.Id, cmd.FileName, parseResult.ErrorMessage);

        // ?? 3. Validate ???????????????????????????????????????????????
        var validateResult = await _mediator.Send(new ValidateFileCommand(file.Id), ct);
        if (!validateResult.Success)
            return Fail(job.Id, file.Id, cmd.FileName, validateResult.ErrorMessage);

        // ?? 4. Persist ????????????????????????????????????????????????
        var persistResult = await _mediator.Send(new PersistFileCommand(file.Id), ct);
        if (!persistResult.Success)
            return Fail(job.Id, file.Id, cmd.FileName, persistResult.ErrorMessage,
                parseResult.TotalRows, validateResult.ValidRowCount, validateResult.InvalidRowCount);

        _log.LogInformation(
            "[UPLOAD_PARR] OK — {File} | {Metrics} métriques | {Valid} lignes valides",
            cmd.FileName, persistResult.MetricsInserted, validateResult.ValidRowCount);

        return new UploadParrFilesResult(
            Success: true,
            JobId: job.Id, FileId: file.Id,
            FileName: cmd.FileName,
            RowsRead: parseResult.TotalRows,
            RowsValid: validateResult.ValidRowCount,
            RowsRejected: validateResult.InvalidRowCount,
            ErrorMessage: null);
    }

    private static UploadParrFilesResult Fail(
        Guid jobId, Guid fileId, string fileName, string? err,
        int rowsRead = 0, int rowsValid = 0, int rowsRejected = 0)
        => new(false, jobId, fileId, fileName, rowsRead, rowsValid, rowsRejected, err);
}
