using MediatR;
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

    /// <summary>
    /// GET /api/reports/{id}/download/pdf
    /// Downloads the PDF file from Blob Storage for the specified report.
    /// </summary>
    [HttpGet("{id:guid}/download/pdf")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadPdf(Guid id, CancellationToken ct = default)
    {
        var report = await reportRepo.GetByIdAsync(id, ct);
        if (report is null)
            return NotFound("Report not found.");

        if (string.IsNullOrEmpty(report.FilePath_PDF))
            return NotFound("PDF file not available for this report.");

        var stream = await blobStorage.DownloadStreamAsync(report.FilePath_PDF, ct);
        var fileName = $"PAFA_{report.ScheduleNumber}_{report.ReportingPeriod:yyyy_MM}.pdf";

        return File(stream, "application/pdf", fileName);
    }

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
