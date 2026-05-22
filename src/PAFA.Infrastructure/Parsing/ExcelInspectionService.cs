using ClosedXML.Excel;
using Microsoft.Extensions.Logging;

namespace PAFA.Infrastructure.Parsing;

/// <summary>
/// Inspects an Excel workbook and extracts the metadata required for the six
/// pipeline validation rules (sheet names, columns, hidden columns, data rows).
/// </summary>
public sealed class ExcelInspectionService
{
    private readonly ILogger<ExcelInspectionService> _log;

    public ExcelInspectionService(ILogger<ExcelInspectionService> log) => _log = log;

    /// <summary>
    /// Opens the provided stream as an XLWorkbook and returns a complete
    /// <see cref="ExcelInspection"/> snapshot.
    /// The caller is responsible for disposing the stream.
    /// </summary>
    public ExcelInspection Inspect(Stream stream, string fileName)
    {
        var sheetNames      = new List<string>();
        var visibleColumns  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hiddenColumns   = new List<HiddenColumnInfo>();
        var dataRows        = new List<ExcelDataRow>();

        try
        {
            using var workbook = new XLWorkbook(stream);

            foreach (var sheet in workbook.Worksheets)
            {
                sheetNames.Add(sheet.Name);

                var range = sheet.RangeUsed();
                if (range is null || range.RowCount() < 2)
                    continue;

                // ── Header row ─────────────────────────────────────────────
                var headerRow   = range.FirstRow();
                var headerCells = headerRow.Cells().ToList();

                foreach (var cell in headerCells)
                {
                    var colName = cell.GetString().Trim();
                    if (string.IsNullOrWhiteSpace(colName))
                        continue;

                    visibleColumns.Add(colName);

                    var xlColumn = sheet.Column(cell.Address.ColumnNumber);
                    if (xlColumn.IsHidden)
                    {
                        hiddenColumns.Add(new HiddenColumnInfo(
                            colName,
                            cell.Address.ColumnNumber,
                            sheet.Name));
                    }
                }

                // ── Data rows ───────────────────────────────────────────────
                foreach (var row in range.RowsUsed().Skip(1))
                {
                    var values = new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase);

                    for (int i = 0; i < headerCells.Count; i++)
                    {
                        var colName = headerCells[i].GetString().Trim();
                        if (string.IsNullOrWhiteSpace(colName))
                            continue;

                        var cell = row.Cell(i + 1);
                        values[colName] = cell.DataType switch
                        {
                            XLDataType.DateTime => cell.GetDateTime()
                                .ToString("yyyy-MM-dd"),
                            XLDataType.Number   => cell.GetDouble()
                                .ToString(System.Globalization.CultureInfo.InvariantCulture),
                            XLDataType.Boolean  => cell.GetBoolean().ToString(),
                            _                   => cell.GetString().Trim()
                        };
                    }

                    dataRows.Add(new ExcelDataRow(row.RowNumber(), sheet.Name, values));
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ExcelInspectionService failed to inspect {FileName}", fileName);
            throw;
        }

        return new ExcelInspection(
            sheetNames,
            visibleColumns.ToList(),
            hiddenColumns,
            dataRows);
    }
}
