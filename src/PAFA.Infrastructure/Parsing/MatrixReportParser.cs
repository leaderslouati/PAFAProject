
namespace PAFA.Infrastructure.Parsing;

public static class MatrixReportParserHelpers
{
    // STRUCTURE RÉELLE (Report_1A - Vacant within month) :
    // Ligne 1 : None, None, "Year/Month"
    // Ligne 2 : None, None, "2025/05", "2025/06", ...
    // Ligne 3 : None, "Shipper Short Code", "Count of sites updated to vacant...", ...
    // Ligne 5 : None, "Total", [valeurs totaux]
    // Lignes 6+ : None, ShortCode, valeurs
    //
    // Algo : skipRows=3, ShortCode=col[1], dates en lignes 2 (format YYYY/MM)
    public static int ParseYearMonth(string ym)
    {
        var parts = ym.Split('/');
        return int.Parse(parts[0]) * 100 + int.Parse(parts[1]);
    }
}

public sealed class MatrixReportParser : IFileParser
{
    public bool CanHandle(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant();
        return name.Contains("VACANT") || name.Contains("VACANT_SITES") || name.Contains("VACANT WITHIN");
    }

    public Task<FileParseResult> ParseAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var excel = new ExcelFileParser();
        return excel.ParseAsync(fileStream, fileName, ct);
    }
}