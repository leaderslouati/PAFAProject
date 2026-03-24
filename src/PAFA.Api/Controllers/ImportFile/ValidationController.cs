using Microsoft.AspNetCore.Mvc;
using PAFA.Domain.IRepository;
using PAFA.Extraction.Commands.Validation;

namespace PAFA.Api.Controllers.ImportFile;

[ApiController]
[Route("api/validation")]
public class ValidationController : ControllerBase
{
    private readonly IIngestionFileRepository _fileRepo;
    private readonly ILogger<ValidationController> _log;

    public ValidationController(
        IIngestionFileRepository fileRepo,
        ILogger<ValidationController> log)
    {
        _fileRepo = fileRepo;
        _log = log;
    }

    /// <summary>
    /// GET /api/validation/{fileId}
    /// Retourne toutes les erreurs de validation pour un fichier donné.
    /// Utilisé par le frontend pour afficher les détails d'erreur après import.
    /// </summary>
    [HttpGet("{fileId:guid}")]
    [ProducesResponseType(typeof(ValidationErrorsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFileValidationErrors(
        Guid fileId, 
        CancellationToken ct = default)
    {
        var file = await _fileRepo.GetByIdAsync(fileId, ct);
        
        if (file == null)
        {
            _log.LogWarning("File not found: {FileId}", fileId);
            return NotFound(new { error = "File not found" });
        }

        // Load validation errors via repository method
        var validationErrors = await _fileRepo.GetValidationErrorsAsync(fileId, ct);

        var errors = validationErrors
            .Select(e => new ValidationErrorDto(
                e.Id,
                e.LineNumber,
                e.ColumnName,
                e.ErrorCode,
                e.ErrorMessage,
                e.OriginalValue,
                e.Severity,
                e.CreatedAt
            ))
            .ToList();

        var response = new ValidationErrorsResponse(
            fileId,
            file.FileName,
            file.ValidationStatus.ToString(),
            file.RowsRead ?? 0,
            file.RowsValid ?? 0,
            file.RowsRejected ?? 0,
            errors.Count,
            errors
        );

        return Ok(response);
    }

    /// <summary>
    /// GET /api/validation/job/{jobId}
    /// Retourne un résumé des erreurs pour tous les fichiers d'un job.
    /// </summary>
    [HttpGet("job/{jobId:guid}")]
    [ProducesResponseType(typeof(JobValidationSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJobValidationSummary(
        Guid jobId,
        CancellationToken ct = default)
    {
        var files = await _fileRepo.GetByJobIdAsync(jobId, ct);

        if (!files.Any())
            return NotFound(new { error = "Job not found or has no files" });

        var summary = files.Select(f => new FileValidationSummary(
            f.Id,
            f.FileName,
            f.ValidationStatus.ToString(),
            f.ErrorCount,
            f.RowsValid ?? 0,
            f.RowsRejected ?? 0
        )).ToList();

        var response = new JobValidationSummaryResponse(
            jobId,
            summary.Count,
            summary.Sum(s => s.TotalErrors),
            summary
        );

        return Ok(response);
    }
}


