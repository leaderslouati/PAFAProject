using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using PAFA.Domain.IRepository;
using PAFA.Extraction.Commands.Export;
using PAFA.Reports.Batch.Models;

namespace PAFA.Reports.Batch.Core;

/// <summary>
/// Concrete implementation of ReportGenerator for Excel format.
/// Uses ClosedXML to generate .xlsx files.
/// </summary>
public class ExcelReportGenerator : ReportGenerator
{
    private readonly IMetricValueRepository _metricRepo;

    public ExcelReportGenerator(
        IMetricValueRepository metricRepo,
        ILogger<ExcelReportGenerator> logger) 
        : base(logger)
    {
        _metricRepo = metricRepo;
    }

    protected override string GetFileExtension() => ".xlsx";

    protected override string GetFileName(ReportGenerationContext context)
        => $"PAFA_Report_{context.Year:D4}_{context.Month:D2}{GetFileExtension()}";

    protected override async Task ValidateContextAsync(
        ReportGenerationContext context, 
        CancellationToken ct)
    {
        var metrics = await _metricRepo.GetFilteredAsync(
            context.Year, 
            context.Month, 
            null, 
            context.ShipperCode, 
            ct);

        if (!metrics.Any())
        {
            Logger.LogWarning(
                "No metrics found for {Year}-{Month:D2}. Will generate empty workbook.",
                context.Year, context.Month);
        }
    }

    protected override async Task GenerateContentAsync(
        ReportGenerationContext context, 
        Stream stream, 
        CancellationToken ct)
    {
        // Fetch data from repository
        var metrics = await _metricRepo.GetFilteredAsync(
            context.Year, 
            context.Month, 
            null, 
            context.ShipperCode, 
            ct);

        // Map to DTO
        var rows = metrics.Select(m => new PowerBiCsvRowDto
        {
            PeriodeDate = m.ReportingPeriod,
            ShipperCode = m.ShipperShortCode,
            ProductClass = null,
            MrfCode = null,
            ReadPerformancePct = null,
            EstimatedReadPct = null,
            AqOverdueCount = null,
            TotalSiteCount = null,
            IsIndustryAverage = false
        }).ToList();

        var props = typeof(PowerBiCsvRowDto).GetProperties();

        // Create workbook
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add($"PAFA Report {context.Year}-{context.Month:D2}");

        // Header row
        for (int i = 0; i < props.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = props[i].Name;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        // Data rows
        for (int r = 0; r < rows.Count; r++)
        {
            for (int c = 0; c < props.Length; c++)
            {
                var value = props[c].GetValue(rows[r]);
                worksheet.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(value);
            }
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        // Freeze header row
        worksheet.SheetView.FreezeRows(1);

        // Save to stream
        workbook.SaveAs(stream);
    }

    protected override void ValidateTempFile(string tempPath)
    {
        base.ValidateTempFile(tempPath);

        var fileInfo = new FileInfo(tempPath);
        
        // Excel files should have minimum size
        if (fileInfo.Length < 4096) // Less than 4KB is suspicious
        {
            Logger.LogWarning(
                "Generated Excel file is very small ({Size} bytes). Might be empty or corrupted.",
                fileInfo.Length);
        }

        // Optional: Validate Excel magic bytes (PK header for ZIP)
        using var fs = File.OpenRead(tempPath);
        var buffer = new byte[4];
        fs.ReadExactly(buffer, 0, 4);
        
        // XLSX files are ZIP archives, should start with PK\x03\x04
        if (buffer[0] != 0x50 || buffer[1] != 0x4B)
        {
            throw new InvalidOperationException(
                $"Generated file is not a valid Excel file (ZIP header missing).");
        }
    }
}
