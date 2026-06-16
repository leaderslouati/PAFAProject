
namespace PAFA.Infrastructure.Parsing;

public static class FlatLongParserHelpers
{
    // Read_Performance_by_Shipper_5.xlsx — Ligne 1 = headers, direct load
    // Colonnes : "AQ Read Performance" | "Topic" | "Shipper Short Code"
    // Topic → MetricKey mapping :
    public static string TopicToMetricKey(string topic)
    {
        return topic.ToUpperInvariant() switch
        {
            var t when t.Contains(">=293") || t.Contains("≥293") => "aq_read_perf_monthly_293k_pct",
            var t when t.Contains("SMART") || t.Contains("AMR") => "aq_read_perf_smart_amr_pct",
            var t when t.Contains("ANNUAL") => "aq_read_perf_annual_pct",
            _ => "aq_read_perf_unknown"
        };
    }
}

public sealed class FlatLongParser : IFileParser
{
    public bool CanHandle(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant();
        return name.Contains("READ_PERFORMANCE") || (name.Contains("READ") && name.Contains("PERF"));
    }

    public Task<FileParseResult> ParseAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        try
        {
            using var wb = new ClosedXML.Excel.XLWorkbook(fileStream);
            var rows = new List<RawDataRow>();

            foreach (var ws in wb.Worksheets)
            {
                var range = ws.RangeUsed();
                if (range is null || range.RowCount() < 2) continue;

                // Read header (first non-empty row)
                var headerRow = range.FirstRow();
                var headers = headerRow.Cells()
                    .Select(c => Normalize(c.GetString()))
                    .ToList();

                // Find relevant column indexes
                int idxValue = headers.FindIndex(h => h.Contains("aqread") || h.Contains("aqreadperformance") || h.Contains("aqreadperformance"));
                if (idxValue < 0)
                    idxValue = headers.FindIndex(h => h.Contains("read") && h.Contains("perf"));

                int idxTopic = headers.FindIndex(h => h.Contains("topic") || h.Contains("reason") );
                int idxSsc = headers.FindIndex(h => h.Contains("shippershortcode") || h.Contains("shortcode") || h.Contains("shipper"));

                if (idxValue < 0 || idxSsc < 0)
                    continue; // sheet not matching expected layout

                int rowNumber = headerRow.RowNumber() + 1;
                foreach (var row in range.RowsUsed().Skip(1))
                {
                    var cells = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                    string ssc = "";
                    string topic = "";
                    string rawValue = "";

                    if (idxSsc >= 0)
                        ssc = row.Cell(idxSsc + 1).GetString().Trim();
                    if (idxTopic >= 0)
                        topic = row.Cell(idxTopic + 1).GetString().Trim();
                    if (idxValue >= 0)
                        rawValue = row.Cell(idxValue + 1).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(ssc) || string.IsNullOrWhiteSpace(rawValue))
                        continue;

                    var metricKey = FlatLongParserHelpers.TopicToMetricKey(topic ?? string.Empty);
                    cells["shippershortcode"] = ssc;
                    cells[metricKey] = rawValue;

                    rows.Add(new RawDataRow
                    {
                        RowNumber = row.RowNumber(),
                        SheetName = ws.Name,
                        Cells = cells
                    });
                }
            }

            return Task.FromResult(new FileParseResult
            {
                Success = true,
                FileName = fileName,
                DetectedFileType = "READ_PERF",
                Rows = rows,
                RowsPerSheet = rows.GroupBy(r => r.SheetName).ToDictionary(g => g.Key, g => g.Count())
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new FileParseResult
            {
                Success = false,
                FileName = fileName,
                ErrorMessage = $"FlatLongParser error: {ex.Message}",
                DetectedFileType = "UNKNOWN"
            });
        }
    }

    private static string Normalize(string raw)
        => raw?.Trim().ToLowerInvariant().Replace(" ", "")
           .Replace("_", "").Replace("-", "") ?? string.Empty;
}
