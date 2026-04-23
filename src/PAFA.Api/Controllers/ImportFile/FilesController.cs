using MediatR;
using Microsoft.AspNetCore.Mvc;
using PAFA.Domain.Enums;
using PAFA.Domain.IRepository;
using PAFA.Extraction.Commands.Import;

namespace PAFA.Api.Controllers.ImportFile;

/// <summary>
/// Ingestion pipeline — single-responsibility endpoints.
///
/// Flux après POST /api/sharepoint/start (background worker) :
///   Le worker exécute automatiquement parse ? validate ? persist.
///   Le front suit la progression via SignalR "StepCompleted"
///   ou en polant GET /api/files/{fileId}/status.
/// </summary>
[ApiController]
[Route("api/files")]
public class FilesController(IMediator mediator, IIngestionFileRepository fileRepo) : ControllerBase
{
    /// <summary>
    /// Retourne l'état des 4 étapes du pipeline pour un fichier.
    /// Utilisé par le front pour l'affichage "Processing Stages".
    ///
    /// Peut être appelé en polling (ex: toutes les 2s) si SignalR n'est pas disponible.
    /// </summary>
    [HttpGet("{fileId:guid}/status")]
    [ProducesResponseType(typeof(FileStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(Guid fileId, CancellationToken ct = default)
    {
        var file = await fileRepo.GetByIdAsync(fileId, ct);
        if (file is null)
            return NotFound(new { error = $"Fichier introuvable : {fileId}" });

        // ?? Mapper IngestionFileStatus + ValidationStatus ? 4 étapes front ??
        var steps = MapToSteps(file.Status, file.ValidationStatus);

        return Ok(new FileStatusResponse(
            FileId:           file.Id,
            FileName:         file.FileName,
            OverallStatus:    file.Status.ToString(),
            ValidationStatus: file.ValidationStatus.ToString(),
            RowsRead:         file.RowsRead,
            RowsValid:        file.RowsValid,
            RowsRejected:     file.RowsRejected,
            DownloadedAt:     file.DownloadedAt,
            ProcessedAt:      file.ProcessedAt,
            Steps:            steps));
    }

    /// <summary>Downloads the file from MinIO and parses it into rows.</summary>
    [HttpPost("{fileId:guid}/parse")]
    [ProducesResponseType(typeof(ParseFileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ParseFileResult), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Parse(Guid fileId, CancellationToken ct = default)
    {
        var result = await mediator.Send(new ParseFileCommand(fileId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Applies business validation rules and persists ValidationError records.</summary>
    [HttpPost("{fileId:guid}/validate")]
    [ProducesResponseType(typeof(ValidateFileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidateFileResult), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Validate(Guid fileId, CancellationToken ct = default)
    {
        var result = await mediator.Send(new ValidateFileCommand(fileId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Inserts MetricValues and moves the blob to processed/failed.</summary>
    [HttpPost("{fileId:guid}/persist")]
    [ProducesResponseType(typeof(PersistFileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PersistFileResult), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Persist(Guid fileId, CancellationToken ct = default)
    {
        var result = await mediator.Send(new PersistFileCommand(fileId), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // ?? Mapping statuts DB ? 4 étapes UI ?????????????????????????????????????

    private static IReadOnlyList<PipelineStepStatus> MapToSteps(
        IngestionFileStatus status, ValidationStatus validationStatus)
    {
        // Step 1 — FileImport : Success dès que l'enregistrement existe en base
        var step1 = new PipelineStepStatus(1, "FileImport", "Success");

        // Step 2 — Parsing
        //   Pending  : Status = Downloaded (pas encore parsé)
        //   Failed   : Status = Failed ET ValidationStatus = Pending (échec au parsing)
        //   Success  : tout autre cas (le fichier a dépassé l'étape parse)
        PipelineStepStatus step2;
        if (status == IngestionFileStatus.Downloaded)
            step2 = new PipelineStepStatus(2, "Parsing", "Pending");
        else if (status == IngestionFileStatus.Failed && validationStatus == ValidationStatus.Pending)
            step2 = new PipelineStepStatus(2, "Parsing", "Failed");
        else
            step2 = new PipelineStepStatus(2, "Parsing", "Success");

        // Step 3 — Validation
        //   Pending  : ValidationStatus = Pending et pas encore en échec de parse
        //   Failed   : ValidationStatus = Failed
        //   Success  : ValidationStatus = Passed | PassedWithWarnings
        PipelineStepStatus step3;
        if (validationStatus == ValidationStatus.Pending && status != IngestionFileStatus.Failed)
            step3 = new PipelineStepStatus(3, "Validation", "Pending");
        else if (validationStatus == ValidationStatus.Failed)
            step3 = new PipelineStepStatus(3, "Validation", "Failed");
        else if (validationStatus is ValidationStatus.Valid or ValidationStatus.PassedWithWarnings)
            step3 = new PipelineStepStatus(3, "Validation", "Success");
        else
            step3 = new PipelineStepStatus(3, "Validation", "Pending");

        // Step 4 — Persistence
        //   Success  : Status = Loaded
        //   Failed   : Status = Failed ET ValidationStatus != Pending (échec à la persistence)
        //   Pending  : validation passée mais pas encore persisté
        PipelineStepStatus step4;
        if (status == IngestionFileStatus.Processed)
            step4 = new PipelineStepStatus(4, "Persistence", "Success");
        else if (status == IngestionFileStatus.Failed
              && validationStatus is ValidationStatus.Valid or ValidationStatus.PassedWithWarnings)
            step4 = new PipelineStepStatus(4, "Persistence", "Failed");
        else
            step4 = new PipelineStepStatus(4, "Persistence", "Pending");

        return [step1, step2, step3, step4];
    }
}

// ?? DTOs réponse ?????????????????????????????????????????????????????????????

/// <summary>État complet des 4 étapes pour un fichier.</summary>
public sealed record FileStatusResponse(
    Guid FileId,
    string FileName,
    string OverallStatus,
    string ValidationStatus,
    int? RowsRead,
    int? RowsValid,
    int? RowsRejected,
    DateTime? DownloadedAt,
    DateTime? ProcessedAt,
    IReadOnlyList<PipelineStepStatus> Steps
);

/// <summary>État d'une étape individuelle.</summary>
public sealed record PipelineStepStatus(
    /// <summary>1=FileImport 2=Parsing 3=Validation 4=Persistence</summary>
    int Step,
    string StepName,
    /// <summary>"Pending" | "Success" | "Failed"</summary>
    string Status
);
