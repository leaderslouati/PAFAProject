using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PAFA.Infrastructure.Parsing;

public static class MultiAxisParserHelpers
{
    // AQ_at_Risk_Mar_2026_For_PAFA.xlsx
    // Ligne 1 = headers : "GWh", "Class 1", "Class 2", "Class 3", "Class 4 >= 293,000", "Class 4 Smart Monthly", "Class 4 Annual", "Total"
    // Ligne 2+ : ShortCode + valeurs par classe
    // PeriodId = déduit du nom de fichier (regex "Mar 2026" → 2026-03-01)
    public static int? ExtractPeriodFromFilename(string fileName)
    {
        var match = Regex.Match(fileName, @"(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\s+(\d{4})",
            RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        var month = ParseMonthAbbr(match.Groups[1].Value);
        var year = int.Parse(match.Groups[2].Value);
        return year * 100 + month;
    }

    private static int ParseMonthAbbr(string m)
    {
        return m.ToLowerInvariant() switch
        {
            "jan" => 1,
            "feb" => 2,
            "mar" => 3,
            "apr" => 4,
            "may" => 5,
            "jun" => 6,
            "jul" => 7,
            "aug" => 8,
            "sep" => 9,
            "oct" => 10,
            "nov" => 11,
            "dec" => 12,
            _ => 0
        };
    }

    public static string HeaderToMetricKey(string header)
    {
        var h = header?.ToLowerInvariant() ?? string.Empty;
        if (h.Contains("gwh")) return "aq_at_risk_gwh";
        if (h.Contains("%") || h.Contains("pct") || h.Contains("percent")) return "aq_at_risk_pct";
        // Fallback: if header refers to total, prefer GWh if contains gwh
        if (h.Contains("total") && h.Contains("gwh")) return "aq_at_risk_gwh";
        if (h.Contains("total") && (h.Contains("%") || h.Contains("pct"))) return "aq_at_risk_pct";
        // Default unknown
        return "aq_at_risk_unknown";
    }

    public static string? HeaderToProductClass(string header)
    {
        var h = header?.ToLowerInvariant() ?? string.Empty;
        if (h.Contains("class 1") || h.Contains("class1")) return "PC1";
        if (h.Contains("class 2") || h.Contains("class2")) return "PC2";
        if (h.Contains("class 3") || h.Contains("class3")) return "PC3";
        if (h.Contains("class 4") || h.Contains("class4")) return "PC4";
        return null;
    }
}

public sealed class MultiAxisParser : IFileParser
{
    public bool CanHandle(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant();
        return (name.Contains("AQ") && name.Contains("RISK")) || name.Contains("AT_RISK") || name.Contains("AQ_AT_RISK");
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

                var headerRow = range.FirstRow();
                var headers = headerRow.Cells().Select(c => c.GetString().Trim()).ToList();

                // Expect first column to be Shipper Short Code
                for (int r = headerRow.RowNumber() + 1; r <= range.LastRowUsed().RowNumber(); r++)
                {
                    var dataRow = ws.Row(r);
                    var ssc = dataRow.Cell(1).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(ssc)) continue;

                    // For each metric column create a separate RawDataRow so MetricValueMapper can set ProductClass
                    for (int c = 2; c <= headers.Count; c++)
                    {
                        var raw = dataRow.Cell(c).GetString().Trim();
                        if (string.IsNullOrWhiteSpace(raw)) continue;

                        var headerText = headers[c - 1];
                        var metricKey = MultiAxisParserHelpers.HeaderToMetricKey(headerText);
                        var productClass = MultiAxisParserHelpers.HeaderToProductClass(headerText);

                        var cells = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["shippershortcode"] = ssc
                        };
                        if (productClass is not null)
                            cells["productclass"] = productClass;
                        cells[metricKey] = raw;

                        rows.Add(new RawDataRow
                        {
                            RowNumber = r,
                            SheetName = ws.Name,
                            Cells = cells
                        });
                    }
                }
            }

            return Task.FromResult(new FileParseResult
            {
                Success = true,
                FileName = fileName,
                DetectedFileType = "AQ_AT_RISK",
                Rows = rows,
                RowsPerSheet = rows.GroupBy(x => x.SheetName).ToDictionary(g => g.Key, g => g.Count())
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new FileParseResult
            {
                Success = false,
                FileName = fileName,
                ErrorMessage = $"MultiAxisParser error: {ex.Message}",
                DetectedFileType = "UNKNOWN"
            });
        }
    }
}
