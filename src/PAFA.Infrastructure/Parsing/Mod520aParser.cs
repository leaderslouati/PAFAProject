using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PAFA.Infrastructure.Parsing; 
public sealed class Mod520aParser : IFileParser
{
    public bool CanHandle(string fileName)
        => Path.GetFileNameWithoutExtension(fileName)
               .ToUpperInvariant().Contains("MOD520A");

    public async Task<FileParseResult> ParseAsync(
        Stream stream, string fileName, CancellationToken ct)
    {
        using var wb = new XLWorkbook(stream);
        var allRows = new List<RawDataRow>();

        foreach (var ws in wb.Worksheets)
        {
            // Chaque feuille correspond à un rapport 2A.x
            var reportCode = DetectReportCode(ws.Name);
            var rows = ParseMatrixSheet(ws, ws.Name, reportCode);
            allRows.AddRange(rows);
        }

        return new FileParseResult
        {
            Success = true,
            FileName = fileName,
            DetectedFileType = "MOD520A",
            Rows = allRows
        };
    }

    private IEnumerable<RawDataRow> ParseMatrixSheet(
        IXLWorksheet ws, string sheetName, string reportCode)
    {
        // STRUCTURE RÉELLE (vérifiée sur le fichier Apr26) :
        // Ligne 1 : vide
        // Ligne 2 : titre (ex: "Estimated & Check Reads used for Gas Allocation for Product Class 1")
        // Ligne 3 : dates (DateTime Excel) — en colonnes à partir de col 2
        // Ligne 4 : sous-métrique ("Est", "Check", "PC1", "PC2", etc.)
        // Ligne 5+ : ShipperShortCode + valeurs

        var range = ws.RangeUsed();
        if (range is null) yield break;

        // Lire la ligne 3 (index 3 = ligne 3, base 1) → header dates
        var dateRow = ws.Row(3);
        var metricRow = ws.Row(4);
        var lastCol = range.LastColumn().ColumnNumber();

        // Construire un header composite : (colIndex → PeriodId, MetricSubKey)
        var headers = new Dictionary<int, (string PeriodId, string SubKey)>();
        for (int c = 2; c <= lastCol; c++)
        {
            var dateCell = dateRow.Cell(c);
            var metaCell = metricRow.Cell(c);
            if (dateCell.IsEmpty()) continue;

            string periodId = dateCell.DataType == XLDataType.DateTime
                ? dateCell.GetDateTime().ToString("yyyy-MM-dd")
                : dateCell.GetString();

            string subKey = metaCell.IsEmpty() ? "" : metaCell.GetString().Trim();
            headers[c] = (periodId, subKey);
        }

        // Détection des blocs PC (séparés par colonne vide)
        // Ex: cols 2-14 = PC1, col 15 = vide, cols 16-28 = PC2
        var blocks = DetectPcBlocks(ws, 3, lastCol);

        // Lignes de données à partir de ligne 5
        int firstDataRow = 5;
        int lastRow = range.LastRow().RowNumber();

        for (int r = firstDataRow; r <= lastRow; r++)
        {
            var row = ws.Row(r);
            var ssc = row.Cell(1).GetString().Trim();
            if (string.IsNullOrEmpty(ssc) || ssc.StartsWith("Industry")) continue;

            foreach (var (colIdx, (periodId, subKey)) in headers)
            {
                var cell = row.Cell(colIdx);
                if (cell.IsEmpty()) continue;
                if (!TryParseDecimal(cell, out var value)) continue;

                // Déterminer le PC depuis le bloc
                var pc = GetProductClass(blocks, colIdx);

                // Construire le MetricKey composite
                var metricKey = BuildMetricKey(sheetName, subKey);

                yield return new RawDataRow
                {
                    RowNumber = r,
                    SheetName = sheetName,
                    Cells = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["shippershortcode"] = ssc,
                        [metricKey] = value.ToString("F6", CultureInfo.InvariantCulture),
                        ["reportingperiod"] = periodId,
                        ["productclasscode"] = pc,
                        ["reportcode"] = reportCode
                    }
                };
            }
        }
    }

    // Mapping nom de feuille → MetricKey(s)
    private static string BuildMetricKey(string sheetName, string subKey)
    {
        var sheet = sheetName.ToUpperInvariant();
        var sub = subKey.ToUpperInvariant();
        return (sheet, sub) switch
        {
            var (s, k) when s.Contains("ESTIMATED") && k == "EST" => "estimated_read_pct",
            var (s, k) when s.Contains("ESTIMATED") && k == "CHECK" => "check_read_count",
            var (s, _) when s.Contains("NO METER") && s.Contains("DATA") => "no_meter_flow_pct",
            var (s, _) when s.Contains("NO METER") => "no_meter_spr_count",
            var (s, _) when s.Contains("TRANSFER") => "transfer_read_succ_pct",
            var (s, _) when s.Contains("READ PERFORM") => "read_performance_pct",
            var (s, _) when s.Contains("VALIDITY") && sub.Contains("MRE01026") => "mre01026_pct",
            var (s, _) when s.Contains("VALIDITY") && sub.Contains("MRE01027") => "mre01027_pct",
            var (s, _) when s.Contains("VALIDITY") && sub.Contains("MRE01028") => "mre01028_pct",
            var (s, _) when s.Contains("VALIDITY") && sub.Contains("MRE01029") => "mre01029_pct",
            var (s, _) when s.Contains("VALIDITY") && sub.Contains("MRE01030") => "mre01030_pct",
            var (s, _) when s.Contains("NO READ") && sub.Contains("1") => "no_read_count_1yr",
            var (s, _) when s.Contains("NO READ") && sub.Contains("2") => "no_read_count_2yr",
            var (s, _) when s.Contains("NO READ") && sub.Contains("3") => "no_read_count_3yr",
            var (s, _) when s.Contains("NO READ") && sub.Contains("4") => "no_read_count_4yr",
            var (s, _) when s.Contains("AQ CORRECTION") => "aq_correction_reason_" + sub.PadLeft(2, '0'),
            var (s, _) when s.Contains("STANDARD") || s.Contains("STD") => "std_corr_factor_count",
            var (s, _) when s.Contains("REPLACED") => "replaced_read_count",
            _ => $"unknown_{sheetName.ToLower().Replace(" ", "_")}"
        };
    }

    // Helpers used by ParseMatrixSheet
    private static string DetectReportCode(string sheetName)
        => sheetName?.Trim().ToUpperInvariant() ?? string.Empty;

    private static List<(int Start, int End, string ProductClass)> DetectPcBlocks(IXLWorksheet ws, int headerRowIndex, int lastCol)
    {
        // Simple conservative block detection: contiguous non-empty columns form a block.
        var blocks = new List<(int, int, string)>();
        int? blockStart = null;
        for (int c = 2; c <= lastCol; c++)
        {
            var cell = ws.Cell(headerRowIndex, c);
            if (!cell.IsEmpty())
            {
                if (blockStart is null) blockStart = c;
            }
            else
            {
                if (blockStart is not null)
                {
                    blocks.Add((blockStart.Value, c - 1, "PC1"));
                    blockStart = null;
                }
            }
        }
        if (blockStart is not null) blocks.Add((blockStart.Value, lastCol, "PC1"));
        if (blocks.Count == 0) blocks.Add((2, lastCol, "PC1"));
        return blocks;
    }

    private static string GetProductClass(List<(int Start, int End, string ProductClass)> blocks, int colIdx)
    {
        foreach (var b in blocks)
            if (colIdx >= b.Start && colIdx <= b.End)
                return b.ProductClass;
        return "PC1";
    }

    private static bool TryParseDecimal(IXLCell cell, out decimal value)
    {
        value = 0m;
        try
        {
            if (cell.DataType == XLDataType.Number)
            {
                value = Convert.ToDecimal(cell.GetDouble());
                return true;
            }
            var s = cell.GetString().Trim();
            if (string.IsNullOrEmpty(s)) return false;
            s = s.Replace("%", "").Trim();
            if (decimal.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d))
            {
                if (d > 0m && d <= 1.0m && s.Contains('.') && d != 1m) d *= 100m;
                value = Math.Round(d, 6);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}