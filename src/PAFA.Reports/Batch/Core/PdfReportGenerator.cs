using Microsoft.Extensions.Logging;
using PAFA.Domain.Contracts;
using PAFA.Domain.IRepository;
using PAFA.Reports.Batch.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PAFA.Reports.Batch.Core;

/// <summary>
/// Concrete implementation of ReportGenerator for PDF format.
/// Uses QuestPDF to generate professional PDF reports.
/// </summary>
public class PdfReportGenerator : ReportGenerator
{
    private readonly IMetricValueRepository _metricRepo;

    public PdfReportGenerator(
        IMetricValueRepository metricRepo,
        ILogger<PdfReportGenerator> logger) 
        : base(logger)
    {
        _metricRepo = metricRepo;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    protected override string GetFileExtension() => ".pdf";

    protected override string GetFileName(ReportGenerationContext context)
        => $"PAFA_Report_{context.Year:D4}_{context.Month:D2}{GetFileExtension()}";

    protected override async Task ValidateContextAsync(
        ReportGenerationContext context, 
        CancellationToken ct)
    {
        // Check if data exists for the period
        var metrics = await _metricRepo.GetFilteredAsync(
            context.Year, 
            context.Month, 
            null, 
            context.ShipperCode, 
            ct);

        if (!metrics.Any())
        {
            Logger.LogWarning(
                "No metrics found for {Year}-{Month:D2}. Will generate empty report.",
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
            ProductClass = null, // TODO: Map from metric
            MrfCode = null,
            ReadPerformancePct = null,
            EstimatedReadPct = null,
            AqOverdueCount = null,
            TotalSiteCount = null,
            IsIndustryAverage = false
        }).ToList();

        // Generate PDF document
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1, Unit.Centimetre);

                // Header
                page.Header().Text($"PAFA PARR Report — {context.Year:D4}-{context.Month:D2}")
                    .SemiBold().FontSize(13).FontColor(Colors.Blue.Medium);

                // Content
                page.Content().Table(table =>
                {
                    var props = typeof(PowerBiCsvRowDto).GetProperties();

                    // Column definitions
                    table.ColumnsDefinition(c =>
                    {
                        foreach (var _ in props) 
                            c.RelativeColumn();
                    });

                    // ? FIX: Call table.Header() ONCE and define all cells inside
                    table.Header(header =>
                    {
                        foreach (var prop in props)
                        {
                            header.Cell()
                                .Background(Colors.Grey.Lighten2)
                                .Padding(5)
                                .Text(prop.Name)
                                .Bold()
                                .FontSize(9);
                        }
                    });

                    // Data rows
                    foreach (var row in rows)
                    {
                        foreach (var prop in props)
                        {
                            var value = prop.GetValue(row)?.ToString() ?? "";
                            table.Cell()
                                .Padding(3)
                                .Text(value)
                                .FontSize(8);
                        }
                    }
                });

                // Footer
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                    x.Span($"  —  Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
                });
            });
        });

        // Write to stream
        document.GeneratePdf(stream);
    }

    protected override void ValidateTempFile(string tempPath)
    {
        base.ValidateTempFile(tempPath);

        var fileInfo = new FileInfo(tempPath);
        
        // PDF files should have minimum size
        if (fileInfo.Length < 1024) // Less than 1KB is suspicious
        {
            Logger.LogWarning(
                "Generated PDF is very small ({Size} bytes). Might be empty or corrupted.",
                fileInfo.Length);
        }

        // Optional: Validate PDF magic bytes
        using var fs = File.OpenRead(tempPath);
        var buffer = new byte[5];
        fs.ReadExactly(buffer, 0, 5);
        var header = System.Text.Encoding.ASCII.GetString(buffer);
        
        if (!header.StartsWith("%PDF-"))
        {
            throw new InvalidOperationException(
                $"Generated file is not a valid PDF. Header: {header}");
        }
    }
}
