using ClosedXML.Excel;
using PAFA.Domain.Interfaces;

namespace PAFA.Infrastructure.Parsing;

public sealed class ExcelFileParser : IFileParser
{
    public bool CanHandle(string fileExtension)
        => fileExtension.ToLowerInvariant() is ".xlsx" or ".xls";

    public Task<FileParseResult> ParseAsync(
        Stream fileStream,
        string fileName,
        CancellationToken ct = default)
    {
        try
        {
            using var workbook = new XLWorkbook(fileStream);

            if (!workbook.Worksheets.Any())
                return Task.FromResult(Fail(fileName, "Le fichier Excel ne contient aucune feuille."));

            var allRows = new List<RawDataRow>();
            var rowsPerSheet = new Dictionary<string, int>();
            var detectedType = DetectFileType(fileName);

            foreach (var sheet in workbook.Worksheets)
            {
                var range = sheet.RangeUsed();
                if (range is null || range.RowCount() < 2)
                {
                    rowsPerSheet[sheet.Name] = 0;
                    continue;
                }

                // ── En-têtes (ligne 1) ────────────────────────────────────
                var headerRow = range.FirstRow();
                var headers = headerRow.Cells()
                    .Select(c => Normalize(c.GetString()))
                    .ToList();

                // ── Lignes de données (ligne 2 → fin) ─────────────────────
                int sheetRowCount = 0;

                foreach (var row in range.RowsUsed().Skip(1))
                {
                    var cells = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

                    for (int i = 0; i < headers.Count; i++)
                    {
                        var cell = row.Cell(i + 1);

                        var value = cell.DataType switch
                        {
                            XLDataType.DateTime => cell.GetDateTime()
                                                       .ToString("yyyy-MM-dd"),
                            XLDataType.Number => cell.GetDouble()
                                                       .ToString(System.Globalization.CultureInfo.InvariantCulture),
                            XLDataType.Boolean => cell.GetBoolean().ToString(),
                            _ => cell.GetString().Trim()
                        };

                        cells[headers[i]] = value;
                    }

                    // Ignorer les lignes complètement vides
                    if (cells.Values.All(v => string.IsNullOrWhiteSpace(v)))
                        continue;

                    allRows.Add(new RawDataRow
                    {
                        RowNumber = row.RowNumber(),
                        SheetName = sheet.Name,
                        Cells = cells
                    });

                    sheetRowCount++;
                }

                rowsPerSheet[sheet.Name] = sheetRowCount;
            }

            return Task.FromResult(new FileParseResult
            {
                Success = true,
                FileName = fileName,
                DetectedFileType = detectedType,
                Rows = allRows,
                RowsPerSheet = rowsPerSheet
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail(fileName, $"Erreur lecture Excel : {ex.Message}"));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Normalise un header : minuscules + suppression espaces.
    /// Ex: "Shipper Short Code" → "shippershortcode"
    /// </summary>
    private static string Normalize(string raw)
        => raw.Trim().ToLowerInvariant().Replace(" ", "");

    /// <summary>
    /// Détecte le type de fichier depuis le nom.
    /// Ex: "MOD520A_Feb25.xlsx" → "MOD520A"
    /// </summary>
    private static string DetectFileType(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant();

        return name switch
        {
            _ when name.Contains("MOD520") => "MOD520A",
            _ when name.Contains("AQ") => "AQ_REPORT",
            _ when name.Contains("NOREADS") => "NO_READS",
            _ when name.Contains("VACANT") => "VACANT_SITES",
            _ when name.Contains("PARR") => "PARR",
            _ => "UNKNOWN"
        };
    }

    private static FileParseResult Fail(string fileName, string error) => new()
    {
        Success = false,
        FileName = fileName,
        ErrorMessage = error,
        DetectedFileType = "UNKNOWN"
    };
}