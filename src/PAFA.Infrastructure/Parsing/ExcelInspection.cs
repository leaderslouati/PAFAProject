namespace PAFA.Infrastructure.Parsing;

/// <summary>
/// Result of inspecting an Excel workbook for pipeline validation purposes.
/// Captures sheet metadata, column inventory, hidden-column findings, and raw data rows.
/// </summary>
public sealed record ExcelInspection(
    IReadOnlyList<string> SheetNames,
    IReadOnlyList<string> VisibleColumns,
    IReadOnlyList<HiddenColumnInfo> HiddenColumns,
    IReadOnlyList<ExcelDataRow> DataRows);

/// <summary>Describes a single hidden column found in the workbook.</summary>
public sealed record HiddenColumnInfo(string ColumnName, int ColumnIndex, string SheetName);

/// <summary>A data row extracted from a worksheet during Excel inspection.</summary>
public sealed record ExcelDataRow(
    int RowNumber,
    string SheetName,
    IReadOnlyDictionary<string, string> Values);
