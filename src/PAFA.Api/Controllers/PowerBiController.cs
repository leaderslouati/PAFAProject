using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PAFA.Domain.Contracts;
using PAFA.Domain.Enums;
using PAFA.Domain.Interfaces;
using PAFA.Domain.IRepository;
using PAFA.Infrastructure.Services.PowerBi;
using PAFA.Reports.Queries;

namespace PAFA.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportController(
    IMediator mediator,
    IEnumerable<IReportWriter> writers,
    IMetricValueRepository metricRepo,
    IReportRepository reportRepo,
    IBlobStorageService blobStorage,
    IPowerBiBatchExportService batchExportService) : ControllerBase
{
    /// <summary>
    /// GET /api/powerbi/export?year=2025&amp;month=2
    /// Génère un CSV pour Power BI avec toutes les métriques de la période.
    /// </summary>
    [HttpGet("powerbi")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportPowerBiCsv(
        [FromQuery] int? year, 
        [FromQuery] int? month, 
        CancellationToken ct = default)
    {
        var query = new ExportPowerBiCsvQuery
        {
            PeriodYear = year,
            PeriodMonth = month
        };

        var stream = await mediator.Send(query, ct);
        var fileName = year.HasValue && month.HasValue
            ? $"PAFA_PowerBI_{year}_{month:D2}.csv"
            : $"PAFA_PowerBI_All.csv";

        return File(stream, "text/csv", fileName);
    }

    [HttpGet("export/pdf")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct = default)
    {
        var writer = writers.FirstOrDefault(w => w.Format == ExportFormat.Pdf);
        if (writer is null) return StatusCode(501, "PdfReportWriter non enregistré.");
        
        // Récupérer les métriques
        var metrics = await metricRepo.GetFilteredAsync(year, month, null, null, ct);
        
        // Mapper vers DTO
        var rows = metrics.Select(m => new PowerBiCsvRowDto
        {
            PeriodeDate = m.ReportingPeriod,
            ShipperCode = m.ShipperShortCode
        }).ToList();
        
        // Générer le fichier PDF
        var stream = await writer.WriteAsync(rows, ct);
        
        var fileName = $"PAFA_Report_{year}_{month:D2}.pdf";
        return File(stream, "application/pdf", fileName);
    }

    [HttpGet("export/excel")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct = default)
    {
        var writer = writers.FirstOrDefault(w => w.Format == ExportFormat.Excel);
        if (writer is null) return StatusCode(501, "ExcelReportWriter non enregistré.");
        
        // Récupérer les métriques
        var metrics = await metricRepo.GetFilteredAsync(year, month, null, null, ct);
        
        // Mapper vers DTO
        var rows = metrics.Select(m => new PowerBiCsvRowDto
        {
            PeriodeDate = m.ReportingPeriod,
            ShipperCode = m.ShipperShortCode
        }).ToList();
        
        // Générer le fichier Excel
        var stream = await writer.WriteAsync(rows, ct);
        
        var fileName = $"PAFA_Report_{year}_{month:D2}.xlsx";
        return File(stream, 
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
            fileName);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  GENERATED REPORTS — List & Download (PDF / PPTX from Blob)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// GET /api/reports?year=2026&amp;month=3
    /// Lists all generated reports for a given period.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListReports(
        [FromQuery] int year, [FromQuery] int month, CancellationToken ct = default)
    {
        var period = new DateOnly(year, month, 1);
        var reports = await reportRepo.GetByPeriodAsync(period, ct);

        var result = reports.Select(r => new
        {
            r.Id,
            r.Title,
            r.ScheduleNumber,
            r.ReportingPeriod,
            Audience = r.Audience.ToString(),
            Status = r.Status.ToString(),
            r.GeneratedAt,
            HasPdf = !string.IsNullOrEmpty(r.FilePath_PDF),
            HasPptx = !string.IsNullOrEmpty(r.FilePath_PPTX),
        });

        return Ok(result);
    }

    /// <summary>
    /// GET /api/reports/{id}/download/pptx
    /// Downloads the PPTX file from Blob Storage for the specified report.
    /// </summary>
    [HttpGet("{id:guid}/download/pptx")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadPptx(Guid id, CancellationToken ct = default)
    {
        var report = await reportRepo.GetByIdAsync(id, ct);
        if (report is null)
            return NotFound("Report not found.");

        if (string.IsNullOrEmpty(report.FilePath_PPTX))
            return NotFound("PPTX file not available for this report.");

        var stream = await blobStorage.DownloadStreamAsync(report.FilePath_PPTX, ct);
        var fileName = $"PAFA_{report.ScheduleNumber}_{report.ReportingPeriod:yyyy_MM}.pptx";

        return File(stream,
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            fileName);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AUDIENCE-SCOPED REPORT ENDPOINTS — Anonymised vs Non-Anonymised
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// GET /api/reports/anonymised?year=2026&amp;month=3
    /// Lists Schedule 2A (Industry, anonymised) reports for the period.
    /// </summary>
    [HttpGet("anonymised")]
    [Authorize(Policy = "CanViewAnonymised")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAnonymisedReports(
        [FromQuery] int year, [FromQuery] int month, CancellationToken ct = default)
    {
        var period  = new DateOnly(year, month, 1);
        var reports = await reportRepo.GetByPeriodAndAudienceAsync(period, ReportAudience.Industry, ct);
        return Ok(MapReportList(reports));
    }

    /// <summary>
    /// GET /api/reports/non-anonymised?year=2026&amp;month=3
    /// Lists Schedule 2B (PAC, non-anonymised) reports for the period.
    /// </summary>
    [HttpGet("non-anonymised")]
    [Authorize(Policy = "CanViewNonAnonymised")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListNonAnonymisedReports(
        [FromQuery] int year, [FromQuery] int month, CancellationToken ct = default)
    {
        var period  = new DateOnly(year, month, 1);
        var reports = await reportRepo.GetByPeriodAndAudienceAsync(period, ReportAudience.PAC, ct);
        return Ok(MapReportList(reports));
    }

    /// <summary>
    /// GET /api/reports/{id}/download?format=pdf|excel|pptx
    /// Downloads a report file from Blob Storage. Requires CanDownload permission.
    /// </summary>
    [HttpGet("{id:guid}/download")]
    [Authorize(Policy = "CanDownload")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadReport(
        Guid id, [FromQuery] string format = "pdf", CancellationToken ct = default)
    {
        var report = await reportRepo.GetByIdAsync(id, ct);
        if (report is null)
            return NotFound("Report not found.");

        var (blobPath, contentType, ext) = format.ToLowerInvariant() switch
        {
            "excel" => (report.FilePath_Excel,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
            "pptx" => (report.FilePath_PPTX,
                "application/vnd.openxmlformats-officedocument.presentationml.presentation", "pptx"),
            _ => (report.FilePath_PDF, "application/pdf", "pdf")
        };

        if (string.IsNullOrEmpty(blobPath))
            return NotFound($"{format.ToUpperInvariant()} file not available for this report.");

        var stream   = await blobStorage.DownloadStreamAsync(blobPath, ct);
        var fileName = $"PAFA_{report.ScheduleNumber}_{report.ReportingPeriod:yyyy_MM}.{ext}";
        return File(stream, contentType, fileName);
    }

    /// <summary>
    /// PUT /api/reports/{id}/observations
    /// Updates the observations text on a report. Permission depends on report audience:
    /// Industry → CanEditAnonymised, PAC → CanEditNonAnonymised.
    /// </summary>
    [HttpPut("{id:guid}/observations")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateObservations(
        Guid id,
        [FromBody] UpdateObservationsRequest request,
        CancellationToken ct = default)
    {
        var report = await reportRepo.GetByIdAsync(id, ct);
        if (report is null)
            return NotFound("Report not found.");

        // Check permission based on report audience
        var requiredPermission = report.Audience == ReportAudience.Industry
            ? "reports.anonymised.edit"
            : "reports.nonanonymised.edit";

        if (!User.HasClaim("permission", requiredPermission))
            return Forbid();

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("sub")?.Value
                     ?? "unknown";

        report.ObservationsText      = request.ObservationsText;
        report.ObservationsBy        = userId;
        report.ObservationsUpdatedAt = DateTime.UtcNow;
        report.UpdatedBy             = userId;
        report.UpdatedAt             = DateTime.UtcNow;

        reportRepo.Update(report);
        // Save via the scoped repository — no UoW needed for single-entity update
        await reportRepo.SaveChangesAsync(ct);

        return Ok(new
        {
            report.Id,
            report.ObservationsText,
            report.ObservationsBy,
            report.ObservationsUpdatedAt
        });
    }

    // ── Private helpers ──────────────────────────────────────────────────

    private static object MapReportList(IReadOnlyList<Domain.Entities.Report> reports)
        => reports.Select(r => new
        {
            r.Id,
            r.Title,
            r.ScheduleNumber,
            r.ReportingPeriod,
            Audience = r.Audience.ToString(),
            Status   = r.Status.ToString(),
            r.GeneratedAt,
            HasPdf  = !string.IsNullOrEmpty(r.FilePath_PDF),
            HasExcel = !string.IsNullOrEmpty(r.FilePath_Excel),
            HasPptx = !string.IsNullOrEmpty(r.FilePath_PPTX),
            r.ObservationsText,
            r.ObservationsBy,
            r.ObservationsUpdatedAt,
        });

  
    // ═══════════════════════════════════════════════════════════════════
    //  MANUAL TRIGGER — For local testing only (no need to wait 1st of month)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// POST /api/reports/export/trigger?year=2026&amp;month=3
    /// Déclenche manuellement l'export Power BI pour une période donnée.
    /// ⚠ DEV / TEST ONLY — désactiver en production.
    /// </summary>
    [HttpPost("export/trigger")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TriggerExport(
        [FromQuery] int year, [FromQuery] int month, CancellationToken ct = default)
    {
        if (year < 2020 || year > 2040) return BadRequest("Année invalide.");
        if (month < 1 || month > 12)    return BadRequest("Mois invalide (1-12).");

        var period = new DateOnly(year, month, 1);

        var result = await batchExportService.ExecuteMonthlyExportAsync(period, ct);

        return Ok(new
        {
            ReportingPeriod = period.ToString("yyyy-MM"),
            result.TotalReports,
            result.Succeeded,
            result.Failed,
            DurationSeconds = result.TotalDuration.TotalSeconds,
            Outcomes = result.Outcomes.Select(o => new
            {
                o.ScheduleRef,
                o.Title,
                o.Success,
                o.BlobPath,
                o.ErrorMessage,
                DurationSeconds = o.Duration.TotalSeconds
            })
        });
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

/// <summary>Payload for PUT /api/reports/{id}/observations.</summary>
public record UpdateObservationsRequest(string? ObservationsText);
