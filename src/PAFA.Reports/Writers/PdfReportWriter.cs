using PAFA.Domain.Enums;
using PAFA.Extraction.Reports.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;


namespace PAFA.Reports.Writers;  

public  class PdfReportWriter : IReportWriter
{
    public ExportFormat Format => ExportFormat.Pdf;

    public Task<Stream> WriteAsync<TDto>(IEnumerable<TDto> data, CancellationToken ct = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var rows = data.ToList();
        var props = typeof(TDto).GetProperties();
        var ms = new MemoryStream();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1, Unit.Centimetre);

                page.Header().Text($"PAFA PARR Report — {DateTime.UtcNow:yyyy-MM-dd}")
                    .SemiBold().FontSize(13).FontColor(Colors.Blue.Medium);

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        foreach (var _ in props) c.RelativeColumn();
                    });

                    // ✓ FIX: Call table.Header() ONCE and define all header cells inside
                    table.Header(header =>
                    {
                        foreach (var p in props)
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text(p.Name).Bold().FontSize(9);
                    });

                    // Données
                    foreach (var row in rows)
                        foreach (var p in props)
                            table.Cell().Padding(3).Text((p.GetValue(row) ?? "").ToString()!).FontSize(8);
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page "); x.CurrentPageNumber(); x.Span(" / "); x.TotalPages();
                });
            });
        }).GeneratePdf(ms);

        ms.Position = 0;
        return Task.FromResult<Stream>(ms);
    }
}
