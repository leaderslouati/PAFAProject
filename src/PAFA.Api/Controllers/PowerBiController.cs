using MediatR;
using Microsoft.AspNetCore.Mvc;
using PAFA.Domain.Enums;
using PAFA.Domain.IRepository;
using PAFA.Extraction.Commands.Export;
using PAFA.Extraction.Reports.Interfaces;
using PAFA.Reports.Queries;

namespace PAFA.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportController(
    IMediator mediator, 
    IEnumerable<IReportWriter> writers, 
    IReportRepository reportRepo,
    IMetricValueRepository metricRepo) : ControllerBase
{
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
}
