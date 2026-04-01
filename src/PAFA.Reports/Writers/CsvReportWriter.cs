using CsvHelper;
using CsvHelper.Configuration;
using PAFA.Domain.Enums;
using PAFA.Domain.Interfaces;
using System.Globalization;

namespace PAFA.Reports.Writers;

/// <summary>
/// IReportWriter implementation for CSV export.
/// Uses CsvHelper with semicolon delimiter (Excel-friendly in European locales).
/// Returns a MemoryStream positioned at 0 — caller owns the lifecycle.
/// </summary>
public  class CsvReportWriter : IReportWriter
{
    public ExportFormat Format => ExportFormat.Csv;

    public async Task<Stream> WriteAsync<TDto>(IEnumerable<TDto> data, CancellationToken ct = default)
    {
        var ms     = new MemoryStream();
        var writer = new StreamWriter(ms, leaveOpen: true);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter       = ";",
            HasHeaderRecord = true
        };

        await using var csv = new CsvWriter(writer, config);

        await csv.WriteRecordsAsync(data, ct);
        await writer.FlushAsync(ct);

        ms.Position = 0;
        return ms;
    }
}
